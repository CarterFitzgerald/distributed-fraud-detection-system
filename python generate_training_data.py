import csv
import math
import random
from dataclasses import dataclass
from typing import List, Tuple

# -----------------------------
# Config
# -----------------------------
SEED = 42
N_ROWS = 10_000

# Fraud percentage range: 0.1% to 0.3%
FRAUD_RATE_MIN = 0.001
FRAUD_RATE_MAX = 0.003

OUTPUT_PATH = "transactions_training.csv"

# Countries you might realistically see; a few treated as higher-risk for synthetic data
COUNTRIES = [
    ("AU", 0.35),
    ("US", 0.25),
    ("GB", 0.10),
    ("NZ", 0.05),
    ("CA", 0.05),
    ("SG", 0.05),
    ("IN", 0.07),
    ("BR", 0.03),
    ("NG", 0.02),
    ("RU", 0.02),
    ("ZA", 0.01),
]

# Map country to currency (simple approximation)
CURRENCY_BY_COUNTRY = {
    "AU": "AUD",
    "NZ": "NZD",
    "US": "USD",
    "CA": "CAD",
    "GB": "GBP",
    "SG": "SGD",
    "IN": "INR",
    "BR": "BRL",
    "NG": "NGN",
    "RU": "RUB",
    "ZA": "ZAR",
}

HIGH_RISK_COUNTRIES = {"NG", "RU"}

# Merchant/customer pool sizes (high-cardinality but not crazy)
N_MERCHANTS = 800
N_CUSTOMERS = 4000

# A small subset of merchants are "riskier" (e.g., chargeback-heavy category)
RISKY_MERCHANT_RATIO = 0.03  # 3%

# -----------------------------
# Helpers
# -----------------------------
def weighted_choice(items: List[Tuple[str, float]], rng: random.Random) -> str:
    r = rng.random()
    cumulative = 0.0
    for value, weight in items:
        cumulative += weight
        if r <= cumulative:
            return value
    return items[-1][0]

def clamp(x: float, lo: float, hi: float) -> float:
    return max(lo, min(hi, x))

def sigmoid(z: float) -> float:
    return 1.0 / (1.0 + math.exp(-z))

@dataclass
class Row:
    amount: float
    country: str
    currency: str
    merchant_id: str
    customer_id: str
    hour_of_day: int
    is_fraud: bool
    risk_score: float  # internal only for selecting fraud rows

def generate_amount(rng: random.Random, is_off_hours: bool, is_high_risk_country: bool) -> float:
    """
    Generate amounts with a realistic heavy tail.
    We use a log-normal-ish approach: exp(N(mu, sigma)).
    Off-hours and high-risk countries slightly shift the distribution upward.
    """
    mu = 3.5     # controls typical amount
    sigma = 1.0  # controls tail heaviness

    # Shift distribution slightly for risk contexts
    if is_off_hours:
        mu += 0.10
    if is_high_risk_country:
        mu += 0.15

    amt = math.exp(rng.normalvariate(mu, sigma))

    # Clamp to practical range
    amt = clamp(amt, 1.00, 25_000.00)

    # Round to cents
    return round(amt, 2)

def hour_distribution(rng: random.Random) -> int:
    """
    Simulate normal purchase behavior:
    - More traffic during day/evening
    - Less traffic late night
    """
    r = rng.random()
    if r < 0.70:
        # Peak hours 9am-10pm
        return rng.randint(9, 22)
    elif r < 0.90:
        # Morning/late evening
        return rng.choice(list(range(6, 9)) + list(range(23, 24)))
    else:
        # Off-hours: 0-5
        return rng.randint(0, 5)

def compute_risk(amount: float, country: str, hour: int, is_risky_merchant: bool) -> float:
    """
    Produce a continuous risk score used to decide which rows become fraud.
    This is NOT the label itself; it's a latent "riskiness" measure for realism.
    """
    score = 0.0

    # Amount contribution (scaled)
    # Typical amounts are smaller; very large amounts add risk.
    score += math.log1p(amount) * 0.6

    # High-risk country bump
    if country in HIGH_RISK_COUNTRIES:
        score += 2.0

    # Off-hours bump
    if 0 <= hour <= 5:
        score += 1.2

    # Risky merchant bump
    if is_risky_merchant:
        score += 1.0

    return score

def main():
    rng = random.Random(SEED)

    # Choose an exact fraud rate within the requested range, then compute exact fraud count
    fraud_rate = rng.uniform(FRAUD_RATE_MIN, FRAUD_RATE_MAX)
    n_fraud = max(1, int(round(N_ROWS * fraud_rate)))  # ensure at least 1

    # Create a risky merchant set
    risky_merchants = set(rng.sample(range(1, N_MERCHANTS + 1), int(N_MERCHANTS * RISKY_MERCHANT_RATIO)))

    rows: List[Row] = []

    for _ in range(N_ROWS):
        country = weighted_choice(COUNTRIES, rng)
        currency = CURRENCY_BY_COUNTRY.get(country, "USD")

        hour = hour_distribution(rng)
        off_hours = 0 <= hour <= 5

        merchant_num = rng.randint(1, N_MERCHANTS)
        customer_num = rng.randint(1, N_CUSTOMERS)

        merchant_id = f"m_{merchant_num:04d}"
        customer_id = f"c_{customer_num:05d}"

        is_risky_merchant = merchant_num in risky_merchants
        is_high_risk_country = country in HIGH_RISK_COUNTRIES

        amount = generate_amount(rng, off_hours, is_high_risk_country)

        risk = compute_risk(amount, country, hour, is_risky_merchant)

        # Temporary label = False; we'll assign fraud later based on top risk
        rows.append(Row(
            amount=amount,
            country=country,
            currency=currency,
            merchant_id=merchant_id,
            customer_id=customer_id,
            hour_of_day=hour,
            is_fraud=False,
            risk_score=risk
        ))

    # Pick fraud rows: mostly the riskiest ones, but allow a little randomness so it's not "too perfect"
    # Sort by risk descending
    rows_sorted = sorted(rows, key=lambda r: r.risk_score, reverse=True)

    # Take top candidates (e.g., top 10% of risk), then sample fraud from them
    candidate_pool_size = max(n_fraud * 10, int(0.10 * N_ROWS))
    candidate_pool = rows_sorted[:candidate_pool_size]

    # Weight selection within candidates by sigmoid(risk) so higher risk more likely
    weights = [sigmoid(r.risk_score - 4.0) for r in candidate_pool]  # shift for reasonable spread
    total_w = sum(weights)
    probs = [w / total_w for w in weights]

    fraud_indices = set()
    while len(fraud_indices) < n_fraud:
        # roulette wheel selection
        x = rng.random()
        cum = 0.0
        for i, p in enumerate(probs):
            cum += p
            if x <= cum:
                fraud_indices.add(i)
                break

    for i in fraud_indices:
        candidate_pool[i].is_fraud = True

    # Write CSV
    with open(OUTPUT_PATH, "w", newline="", encoding="utf-8") as f:
        writer = csv.writer(f)
        writer.writerow(["Amount", "Country", "Currency", "MerchantId", "CustomerId", "HourOfDay", "IsFraud"])
        for r in rows:
            writer.writerow([f"{r.amount:.2f}", r.country, r.currency, r.merchant_id, r.customer_id, r.hour_of_day, str(r.is_fraud).lower()])

    fraud_count = sum(1 for r in rows if r.is_fraud)
    print(f"Wrote {N_ROWS} rows to {OUTPUT_PATH}")
    print(f"Fraud rows: {fraud_count} ({fraud_count / N_ROWS * 100:.3f}%)")
    print(f"Target fraud rate was ~{fraud_rate * 100:.3f}% (exact fraud rows chosen: {n_fraud})")

if __name__ == "__main__":
    main()
