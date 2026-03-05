# fraud_data_generator.py
#
# Synthetic fraud dataset generator for Option A
# GOAL: Avoid "perfect" results by preventing stacked signal leakage.
#
# Key changes vs your version:
# 1) Risk-budgeted fraud generation: fraud rows only activate 1–3 strong signals, not ALL of them.
# 2) Legit "lookalikes" (false positives): a small % of legit rows are generated in risky regimes.
# 3) Fraud "camouflage" (false negatives): a small % of fraud rows are generated in normal regimes.
# 4) Fraud ring pools look like normal IDs AND are different for train vs test (prevents memorization).
# 5) Engineered flags + RiskScore are computed from features but DO NOT deterministically imply IsFraud.
#
# Output columns (CSV header):
# TransactionId,Amount,Country,Currency,MerchantId,CustomerId,DeviceId,PaymentMethodToken,
# HourOfDay,DayOfWeek,TransactionType,Channel,MerchantCategory,DeviceType,
# IsNewDevice,IsNewPaymentToken,IsInternational,AccountAgeDays,CustomerAge,
# TxnCountLast1h,TxnCountLast24h,TotalAmountLast24h,PaymentMethodAgeDays,DistanceFromHomeKm,MccRisk,
# IsHighRiskMcc,IsHighVelocity,IsSuspiciousCombo,IsFraud,RiskScore
#
# Tips:
# - Keep engineered flags in CSV, but start training WITHOUT them (as you already do).
# - If metrics are still too good, increase overlap:
#   - raise FRAUD_CAMOUFLAGE_RATE (more fraud that looks normal)
#   - raise LEGIT_LOOKALIKE_RATE (more legit that looks risky)
#   - reduce FRAUD_SIGNAL_BUDGET_MAX (fewer stacked fraud signals)

import csv
import os
import random
import uuid
import math
from collections import defaultdict

# -------------------------
# Config
# -------------------------
TRAIN_ROWS = 1_000_000
TEST_ROWS = 200_000
FRAUD_RATE = 0.0025  # 0.25%

SEED = 42
OUT_DIR = "FraudModelTrainer.OptionA/Data"
TRAIN_FILE = os.path.join(OUT_DIR, "transactions_train.csv")
TEST_FILE  = os.path.join(OUT_DIR, "transactions_test.csv")

# Controls how learnable fraud is. Keep near 1.0 for realism.
FRAUD_STRENGTH = 1.00  # try 0.90..1.10

# Fraud rings: reused devices/tokens/merchants across some fraud txns
ENABLE_FRAUD_RINGS = True
FRAUD_RING_SHARE = 0.25  # lower than before to reduce easy memorization

# Legit bursts: occasional legit sessions with high velocity/amount (prevents velocity == fraud)
LEGIT_BURST_RATE = 0.012  # ~1.2%

# Overlap controls (fraud sometimes looks normal)
FRAUD_USE_HOME_COUNTRY_RATE = 0.30
FRAUD_USE_KNOWN_DEVICE_RATE = 0.70
FRAUD_USE_KNOWN_TOKEN_RATE = 0.65
FRAUD_USE_PREFERRED_MERCHANT_RATE = 0.45

# NEW: explicitly create some FPs/FNs at the data level (realistic ambiguity)
LEGIT_LOOKALIKE_RATE = 0.003   # 1.0% of legit rows are generated like "risky legit" (likely FPs)
FRAUD_CAMOUFLAGE_RATE = 0.05   # 18% of fraud rows are generated like "normal fraud" (likely FNs)

# NEW: prevent stacked signals from making fraud trivial
FRAUD_SIGNAL_BUDGET_MIN = 1    # fraud activates at least 1 strong signal
FRAUD_SIGNAL_BUDGET_MAX = 3    # ...and at most 2 (critical knob!)

random.seed(SEED)

# -------------------------
# Reference data
# -------------------------
COUNTRIES = [
    ("US", "USD"), ("CA", "CAD"), ("GB", "GBP"), ("IE", "EUR"), ("FR", "EUR"),
    ("DE", "EUR"), ("NL", "EUR"), ("BE", "EUR"), ("ES", "EUR"), ("PT", "EUR"),
    ("IT", "EUR"), ("CH", "CHF"), ("AT", "EUR"), ("SE", "SEK"), ("NO", "NOK"),
    ("DK", "DKK"), ("FI", "EUR"), ("PL", "PLN"), ("CZ", "CZK"), ("HU", "HUF"),
    ("RO", "RON"), ("GR", "EUR"), ("TR", "TRY"), ("RU", "RUB"), ("UA", "UAH"),
    ("AU", "AUD"), ("NZ", "NZD"), ("JP", "JPY"), ("KR", "KRW"), ("CN", "CNY"),
    ("HK", "HKD"), ("SG", "SGD"), ("IN", "INR"), ("ID", "IDR"), ("MY", "MYR"),
    ("TH", "THB"), ("VN", "VND"), ("PH", "PHP"), ("BR", "BRL"), ("MX", "MXN"),
    ("AR", "ARS"), ("CL", "CLP"), ("CO", "COP"), ("ZA", "ZAR"), ("NG", "NGN"),
    ("KE", "KES"), ("EG", "EGP"), ("SA", "SAR"), ("AE", "AED"), ("IL", "ILS"),
]
COUNTRY_TO_CURRENCY = {c: cur for c, cur in COUNTRIES}
COUNTRY_CODES = [c for c, _ in COUNTRIES]

HIGH_RISK_COUNTRIES = {"NG", "RU", "UA", "TR", "BR", "AR", "EG"}

TRANSACTION_TYPES = [
    "CARD_CREDIT", "CARD_DEBIT", "BANK_TRANSFER", "DIRECT_DEBIT", "CHEQUE", "CASH_WITHDRAWAL"
]
CHANNELS = ["ECOM", "IN_STORE", "MOTO", "ATM"]
MERCHANT_CATEGORIES = [
    "GROCERY", "ELECTRONICS", "FUEL", "TRAVEL", "GAMING", "LUXURY",
    "RESTAURANT", "PHARMACY", "SUBSCRIPTION", "DIGITAL_GOODS",
    "GIFT_CARDS", "CRYPTO", "MONEY_TRANSFER"
]
DEVICE_TYPES = ["MOBILE", "DESKTOP", "TABLET", "POS_TERMINAL", "ATM"]

MCC_RISK = {
    "GROCERY": 0.05,
    "RESTAURANT": 0.08,
    "FUEL": 0.10,
    "PHARMACY": 0.12,
    "SUBSCRIPTION": 0.15,
    "ELECTRONICS": 0.22,
    "TRAVEL": 0.25,
    "DIGITAL_GOODS": 0.30,
    "GAMING": 0.35,
    "LUXURY": 0.40,
    "GIFT_CARDS": 0.65,
    "CRYPTO": 0.80,
    "MONEY_TRANSFER": 0.85,
}
HIGH_RISK_MCCS = {"GIFT_CARDS", "CRYPTO", "MONEY_TRANSFER", "DIGITAL_GOODS", "GAMING"}

# -------------------------
# ID pools
# -------------------------
NUM_CUSTOMERS = 200_000
NUM_MERCHANTS = 50_000

CUSTOMERS = [f"c_{i:06d}" for i in range(NUM_CUSTOMERS)]
MERCHANTS = [f"m_{i:06d}" for i in range(NUM_MERCHANTS)]

# Fraud ring pools (shared artifacts) – initialized per dataset
FRAUD_RING_DEVICES = []
FRAUD_RING_TOKENS = []
FRAUD_RING_MERCHANTS = []


def init_fraud_ring_pools(seed: int):
    """
    Create fraud ring pools that look like normal IDs.
    IMPORTANT: seed per dataset so ring IDs do not repeat across train/test.
    """
    global FRAUD_RING_DEVICES, FRAUD_RING_TOKENS, FRAUD_RING_MERCHANTS
    rnd = random.Random(seed)

    # Devices look like normal device ids: d_<hex>
    FRAUD_RING_DEVICES = ["d_" + uuid.UUID(int=rnd.getrandbits(128)).hex[:10] for _ in range(500)]
    # Tokens look like normal tokens: pm_<hex>
    FRAUD_RING_TOKENS = ["pm_" + uuid.UUID(int=rnd.getrandbits(128)).hex[:12] for _ in range(500)]
    # Merchants: pick a stable-looking subset of real merchants
    FRAUD_RING_MERCHANTS = rnd.sample(MERCHANTS, 300)


# -------------------------
# Output schema
# -------------------------
FIELDNAMES = [
    "TransactionId",
    "Amount",
    "Country",
    "Currency",
    "MerchantId",
    "CustomerId",
    "DeviceId",
    "PaymentMethodToken",
    "HourOfDay",
    "DayOfWeek",
    "TransactionType",
    "Channel",
    "MerchantCategory",
    "DeviceType",
    "IsNewDevice",
    "IsNewPaymentToken",
    "IsInternational",
    "AccountAgeDays",
    "CustomerAge",
    "TxnCountLast1h",
    "TxnCountLast24h",
    "TotalAmountLast24h",
    "PaymentMethodAgeDays",
    "DistanceFromHomeKm",
    "MccRisk",
    "IsHighRiskMcc",
    "IsHighVelocity",
    "IsSuspiciousCombo",
    "IsFraud",
    "RiskScore",
]

# -------------------------
# Utils
# -------------------------
def clamp(x, lo, hi):
    return lo if x < lo else hi if x > hi else x


def weighted_choice(items, weights):
    total = float(sum(weights))
    r = random.uniform(0.0, total)
    upto = 0.0
    for item, w in zip(items, weights):
        upto += float(w)
        if upto >= r:
            return item
    return items[-1]


def is_off_hours(hour: int) -> bool:
    return (hour <= 5) or (hour >= 23)


def rand_day_of_week() -> int:
    return random.randint(0, 6)


def rand_hour(mode: str) -> int:
    """
    mode: "normal", "risky"
    """
    if mode == "risky":
        # skew off-hours, but not exclusively
        if random.random() < 0.55:
            return random.choice([0, 1, 2, 3, 4, 5, 22, 23])
        return random.randint(8, 21)

    # normal
    if random.random() < 0.75:
        return random.randint(8, 21)
    return random.choice([0, 1, 2, 3, 4, 5, 22, 23])


def lognormal_amount() -> float:
    amt = random.lognormvariate(mu=3.8, sigma=0.9)
    return round(min(max(amt, 0.5), 50_000.0), 2)


def payment_token() -> str:
    return "pm_" + uuid.uuid4().hex[:12]


def make_device_id() -> str:
    return "d_" + uuid.uuid4().hex[:10]


def maybe_currency_mismatch(country_code: str, mode: str) -> str:
    expected = COUNTRY_TO_CURRENCY[country_code]
    if mode == "risky":
        # keep modest; mismatch isn't a guaranteed fraud tell
        if random.random() < clamp(0.18 * FRAUD_STRENGTH, 0.10, 0.30):
            return random.choice(list(set(COUNTRY_TO_CURRENCY.values())))
        return expected

    # normal
    if random.random() < 0.94:
        return expected
    return random.choice(list(set(COUNTRY_TO_CURRENCY.values())))


# -------------------------
# Stable customer profiles
# -------------------------
HOME_COUNTRY_WEIGHTS = {c: 1 for c in COUNTRY_CODES}
for c in ["US", "GB", "AU", "DE", "FR", "CA", "JP", "IN", "SG"]:
    HOME_COUNTRY_WEIGHTS[c] += 4


def pick_home_country() -> str:
    items = list(HOME_COUNTRY_WEIGHTS.keys())
    weights = [HOME_COUNTRY_WEIGHTS[c] for c in items]
    return weighted_choice(items, weights)


CUSTOMER_HOME_COUNTRY = {}
CUSTOMER_ACCOUNT_AGE_DAYS = {}
CUSTOMER_AGE = {}
CUSTOMER_DAILY_ACTIVITY = {}
CUSTOMER_HOURLY_ACTIVITY = {}
CUSTOMER_HOME_DISTANCE_KM = {}
CUSTOMER_DEVICES = defaultdict(list)
CUSTOMER_TOKENS = defaultdict(list)
CUSTOMER_PREF_MERCHANTS = defaultdict(list)

for cust in CUSTOMERS:
    home = pick_home_country()
    CUSTOMER_HOME_COUNTRY[cust] = home
    CUSTOMER_ACCOUNT_AGE_DAYS[cust] = random.randint(0, 3650)
    CUSTOMER_AGE[cust] = random.randint(18, 85)

    daily = max(1, int(random.lognormvariate(0.6, 0.7)))
    daily = min(daily, 60)
    CUSTOMER_DAILY_ACTIVITY[cust] = daily
    CUSTOMER_HOURLY_ACTIVITY[cust] = max(1, int(daily / 6))

    base_dist = random.lognormvariate(2.2, 0.6)  # median ~9km
    CUSTOMER_HOME_DISTANCE_KM[cust] = min(base_dist, 200.0)

    for _ in range(random.choice([1, 1, 2, 2, 3])):
        CUSTOMER_DEVICES[cust].append(make_device_id())
    for _ in range(random.choice([1, 2, 2, 3, 4])):
        CUSTOMER_TOKENS[cust].append(payment_token())

    pref = set()
    while len(pref) < 10:
        pref.add(random.choice(MERCHANTS))
    CUSTOMER_PREF_MERCHANTS[cust] = list(pref)

# -------------------------
# Conditional picks (now driven by MODE, not label)
# -------------------------
def pick_country(home_country: str, mode: str) -> str:
    if mode == "normal":
        if random.random() < 0.78:
            return home_country
        common = ["US", "GB", "AU", "DE", "FR", "CA", "JP", "IN", "SG", "NL", "ES", "IT"]
        if random.random() < 0.60:
            return random.choice(common)
        return random.choice(COUNTRY_CODES)

    # risky
    # more travel + more high-risk, but not guaranteed
    if random.random() < 0.20:
        return home_country
    items = COUNTRY_CODES
    weights = []
    for c in items:
        w = 1.0
        if c in HIGH_RISK_COUNTRIES:
            w *= 4.0
        if c != home_country:
            w *= 1.7
        weights.append(w)
    return weighted_choice(items, weights)


def pick_channel(mode: str) -> str:
    if mode == "risky":
        return weighted_choice(CHANNELS, [5, 2, 3, 1])  # ECOM/MOTO heavier
    return weighted_choice(CHANNELS, [2, 7, 1, 1])


def pick_mcc(mode: str) -> str:
    if mode == "risky":
        weights = [4.0 if m in HIGH_RISK_MCCS else 1.0 for m in MERCHANT_CATEGORIES]
        return weighted_choice(MERCHANT_CATEGORIES, weights)
    weights = [1.0 if m in {"GIFT_CARDS", "CRYPTO", "MONEY_TRANSFER"} else 4.0 for m in MERCHANT_CATEGORIES]
    return weighted_choice(MERCHANT_CATEGORIES, weights)


def pick_txn_type(mode: str) -> str:
    if mode == "risky":
        return weighted_choice(TRANSACTION_TYPES, [3, 2, 4, 1, 1, 3])
    return weighted_choice(TRANSACTION_TYPES, [5, 5, 2, 4, 1, 1])


def pick_device(customer: str, mode: str, allow_ring: bool):
    """
    returns (device_id, is_new_device, device_type)
    """
    if allow_ring and ENABLE_FRAUD_RINGS and (random.random() < FRAUD_RING_SHARE):
        device_id = random.choice(FRAUD_RING_DEVICES)
        device_type = weighted_choice(DEVICE_TYPES, [4, 3, 2, 1, 1])
        return device_id, True, device_type

    if mode == "risky":
        # higher new-device probability
        if random.random() < 0.10:
            device_id = make_device_id()
            CUSTOMER_DEVICES[customer].append(device_id)
            is_new = True
        else:
            device_id = random.choice(CUSTOMER_DEVICES[customer])
            is_new = False
    else:
        if random.random() < 0.035:
            device_id = make_device_id()
            CUSTOMER_DEVICES[customer].append(device_id)
            is_new = True
        else:
            device_id = random.choice(CUSTOMER_DEVICES[customer])
            is_new = False

    device_type = weighted_choice(DEVICE_TYPES, [4, 3, 2, 2, 1])
    return device_id, is_new, device_type


def pick_payment_token(customer: str, mode: str, allow_ring: bool):
    """
    returns (token, is_new_token, pm_age_days)
    """
    if allow_ring and ENABLE_FRAUD_RINGS and (random.random() < FRAUD_RING_SHARE):
        token = random.choice(FRAUD_RING_TOKENS)
        pm_age_days = random.choice([0, 0, 1, 1, 2, 3, 5, 7])
        return token, True, pm_age_days

    if mode == "risky":
        if random.random() < 0.12:
            token = payment_token()
            CUSTOMER_TOKENS[customer].append(token)
            is_new = True
            pm_age_days = random.choice([0, 0, 1, 1, 2, 3, 5, 7, 10, 14])
        else:
            token = random.choice(CUSTOMER_TOKENS[customer])
            is_new = False
            pm_age_days = int(clamp(random.lognormvariate(2.2, 1.0), 0, 3650))
            if random.random() < 0.12:
                pm_age_days = random.randint(10, 180)
    else:
        if random.random() < 0.05:
            token = payment_token()
            CUSTOMER_TOKENS[customer].append(token)
            is_new = True
            pm_age_days = random.choice([0, 1, 2, 3, 5, 7, 10, 14])
        else:
            token = random.choice(CUSTOMER_TOKENS[customer])
            is_new = False
            pm_age_days = int(clamp(random.lognormvariate(2.2, 1.0), 0, 3650))

    return token, is_new, pm_age_days


def pick_merchant(customer: str, mode: str, allow_ring: bool) -> str:
    if allow_ring and ENABLE_FRAUD_RINGS and (random.random() < FRAUD_RING_SHARE):
        return random.choice(FRAUD_RING_MERCHANTS)

    if mode == "normal":
        if random.random() < 0.85:
            return random.choice(CUSTOMER_PREF_MERCHANTS[customer])
        return random.choice(MERCHANTS)

    # risky: more unfamiliar merchants, but still some preferred overlap
    if random.random() < 0.35:
        return random.choice(CUSTOMER_PREF_MERCHANTS[customer])
    return random.choice(MERCHANTS)


def compute_distance_km(customer: str, country: str, home_country: str, mode: str) -> float:
    base = CUSTOMER_HOME_DISTANCE_KM[customer]
    if country != home_country:
        dist = random.lognormvariate(6.2, 0.7)  # international
        if mode == "risky" and random.random() < 0.18:
            dist *= random.uniform(1.1, 1.8)
        return float(clamp(dist, 0.0, 20000.0))

    dist = random.lognormvariate(math.log(max(base, 1.0)), 0.5)
    # rare domestic outliers
    if mode == "risky" and random.random() < 0.08:
        dist *= random.uniform(3.0, 12.0)
    elif mode == "normal" and random.random() < 0.0025:
        dist *= random.uniform(3.0, 10.0)
    return float(clamp(dist, 0.0, 20000.0))


def compute_velocity(customer: str, amount: float, channel: str, mcc: str, hour: int, mode: str):
    base_daily = CUSTOMER_DAILY_ACTIVITY[customer]
    base_hourly = CUSTOMER_HOURLY_ACTIVITY[customer]

    txn1h = max(0, int(random.gauss(base_hourly, max(1.0, base_hourly * 0.6))))
    txn24h = max(txn1h, int(random.gauss(base_daily, max(1.0, base_daily * 0.7))))

    # context bumps (both modes)
    if channel in ("ECOM", "MOTO") and is_off_hours(hour):
        txn1h += random.randint(0, 2)
        txn24h += random.randint(0, 6)
    if MCC_RISK.get(mcc, 0.2) >= 0.6:
        txn1h += random.randint(0, 2)
        txn24h += random.randint(0, 5)

    # legit bursts apply in NORMAL too
    if mode == "normal" and (random.random() < LEGIT_BURST_RATE):
        txn1h += random.randint(3, 9)
        txn24h += random.randint(8, 25)

    # risky mode adds additional bump but not extreme
    if mode == "risky":
        txn1h += random.randint(1, int(5 * FRAUD_STRENGTH))
        txn24h += random.randint(3, int(15 * FRAUD_STRENGTH))

    amt24h = max(0.0, random.gauss(txn24h * (amount * 0.35), amount * 2.0))
    if mode == "risky":
        amt24h += random.uniform(200, 5000) * FRAUD_STRENGTH
    elif random.random() < 0.01:
        amt24h += random.uniform(200, 4000)

    amt24h = float(clamp(amt24h, 0.0, 200_000.0))
    return txn1h, txn24h, amt24h


def maybe_bump_amount(amount: float, mode: str, mcc: str, txn_type: str) -> float:
    a = float(amount)
    if mode == "risky":
        if random.random() < (0.28 * FRAUD_STRENGTH):
            a = clamp(a * random.uniform(1.5, 4.0), 0.5, 50_000.0)
        if mcc in {"GIFT_CARDS", "CRYPTO"} and random.random() < (0.22 * FRAUD_STRENGTH):
            step = random.choice([25, 50, 100, 200, 500])
            a = round(round(a / step) * step, 2)
        if txn_type == "BANK_TRANSFER" and random.random() < (0.20 * FRAUD_STRENGTH):
            a = clamp(a + random.uniform(200, 2500), 0.5, 50_000.0)
    else:
        if random.random() < 0.02:
            a = clamp(a * random.uniform(2.0, 4.0), 0.5, 50_000.0)
    return round(float(a), 2)


def compute_risk_score(
    amount, country, home_country, hour, new_device, channel, txn_type, currency, mcc,
    pm_age_days, dist_km, txn1h, txn24h, amt24h, new_token
) -> int:
    score = 0
    if amount >= 5000: score += 4
    elif amount >= 2000: score += 3
    elif amount >= 500: score += 2

    if country in HIGH_RISK_COUNTRIES: score += 2
    if country != home_country: score += 2

    if is_off_hours(hour): score += 1
    if new_device: score += 2
    if new_token: score += 2

    if channel in ("ECOM", "MOTO"): score += 1
    if txn_type in ("BANK_TRANSFER", "CASH_WITHDRAWAL"): score += 1

    expected_currency = COUNTRY_TO_CURRENCY[country]
    if currency != expected_currency: score += 2

    if pm_age_days <= 2: score += 2
    elif pm_age_days <= 14: score += 1

    if dist_km >= 500: score += 2
    elif dist_km >= 100: score += 1

    if txn1h >= 6: score += 2
    elif txn1h >= 3: score += 1

    if txn24h >= 25: score += 2
    elif txn24h >= 10: score += 1

    if amt24h >= 4000: score += 2
    elif amt24h >= 1500: score += 1

    mcc_r = MCC_RISK.get(mcc, 0.2)
    if mcc_r >= 0.6: score += 2
    elif mcc_r >= 0.35: score += 1

    return int(score)


# -------------------------
# NEW: risk-budgeted MODE selection (creates FPs/FNs and avoids stacked signals)
# -------------------------
def choose_mode_for_row(is_fraud_label: bool) -> str:
    """
    Returns "normal" or "risky".
    - Most legit rows are normal, but LEGIT_LOOKALIKE_RATE are risky (FP-prone).
    - Most fraud rows are risky, but FRAUD_CAMOUFLAGE_RATE are normal (FN-prone).
    """
    if is_fraud_label:
        if random.random() < FRAUD_CAMOUFLAGE_RATE:
            return "normal"
        return "risky"
    else:
        if random.random() < LEGIT_LOOKALIKE_RATE:
            return "risky"
        return "normal"


def apply_fraud_signal_budget(mode: str, is_fraud_label: bool) -> dict:
    """
    For fraud rows in risky mode, only enable 1..N strong signals (prevents stacking).
    For other rows, keep mostly normal behavior.
    Returns a dict of toggles that downstream functions can use.
    """
    toggles = {
        "force_risky_geo": False,
        "force_new_device": False,
        "force_new_token": False,
        "force_velocity_bump": False,
        "force_amount_bump": False,
        "allow_ring": False,
    }

    if not is_fraud_label or mode != "risky":
        # For legit risky-lookalikes, we DON'T turn everything on; just let mode do mild bumps.
        # For fraud camouflage, we keep things looking normal.
        toggles["allow_ring"] = False
        return toggles

    # Fraud + risky: choose a limited number of strong signals
    budget = random.randint(FRAUD_SIGNAL_BUDGET_MIN, FRAUD_SIGNAL_BUDGET_MAX)

    # signal candidates (weights)
    candidates = [
        ("force_new_device", 1.2),
        ("force_new_token", 1.2),
        ("force_velocity_bump", 1.0),
        ("force_amount_bump", 0.9),
        ("force_risky_geo", 0.8),
        ("allow_ring", 0.7 if ENABLE_FRAUD_RINGS else 0.0),
    ]
    # normalize to a sampling list
    keys = [k for k, w in candidates if w > 0]
    weights = [w for k, w in candidates if w > 0]

    chosen = set()
    while len(chosen) < budget and keys:
        k = weighted_choice(keys, weights)
        chosen.add(k)

    for k in chosen:
        toggles[k] = True

    # Even with ring allowed, don't always use it
    if toggles["allow_ring"] and random.random() > 0.65:
        toggles["allow_ring"] = False

    return toggles


# -------------------------
# Row generation (label-first + mode + budget)
# -------------------------
def sample_row(txn_id_prefix: str, txn_seq: int, is_fraud_label: bool):
    customer = random.choice(CUSTOMERS)
    home_country = CUSTOMER_HOME_COUNTRY[customer]

    mode = choose_mode_for_row(is_fraud_label)
    sig = apply_fraud_signal_budget(mode, is_fraud_label)

    # Geo: if force_risky_geo, bias away from home + toward high-risk
    if sig["force_risky_geo"]:
        country = pick_country(home_country, "risky")
    else:
        country = pick_country(home_country, mode)

    channel = pick_channel(mode)
    mcc = pick_mcc(mode)
    txn_type = pick_txn_type(mode)
    hour = rand_hour(mode)
    dow = rand_day_of_week()

    merchant = pick_merchant(customer, mode, allow_ring=sig["allow_ring"])

    # device/token: if forced, bias to new, but still some overlap realism
    device_id, is_new_device, device_type = pick_device(customer, mode, allow_ring=sig["allow_ring"])
    token, is_new_token, pm_age_days = pick_payment_token(customer, mode, allow_ring=sig["allow_ring"])

    if sig["force_new_device"]:
        # create a new device and mark new
        device_id = make_device_id()
        CUSTOMER_DEVICES[customer].append(device_id)
        is_new_device = True

    if sig["force_new_token"]:
        token = payment_token()
        CUSTOMER_TOKENS[customer].append(token)
        is_new_token = True
        pm_age_days = random.choice([0, 0, 1, 1, 2, 3, 5, 7, 10, 14])

    # currency
    currency = maybe_currency_mismatch(country, mode)

    # amount
    amount = lognormal_amount()
    if sig["force_amount_bump"]:
        amount = maybe_bump_amount(amount, "risky", mcc, txn_type)
    else:
        amount = maybe_bump_amount(amount, mode, mcc, txn_type)

    # distance
    dist_km = compute_distance_km(customer, country, home_country, mode)

    # velocity
    if sig["force_velocity_bump"]:
        txn1h, txn24h, amt24h = compute_velocity(customer, amount, channel, mcc, hour, "risky")
    else:
        txn1h, txn24h, amt24h = compute_velocity(customer, amount, channel, mcc, hour, mode)

    # derived flags
    is_international = (country != home_country)
    mcc_risk = float(MCC_RISK.get(mcc, 0.2))
    is_high_risk_mcc = (mcc_risk >= 0.6)
    is_high_velocity = (txn1h >= 6) or (txn24h >= 25) or (amt24h >= 4000)

    # suspicious combo: NOT guaranteed for fraud; depends on mode + chance
    suspicious_base = 0.16 if mode == "normal" else 0.35
    if is_off_hours(hour) and (channel in ("ECOM", "MOTO")) and (is_new_device or is_new_token):
        is_suspicious_combo = (random.random() < suspicious_base)
    else:
        is_suspicious_combo = False

    risk_score = compute_risk_score(
        amount=amount,
        country=country,
        home_country=home_country,
        hour=hour,
        new_device=is_new_device,
        channel=channel,
        txn_type=txn_type,
        currency=currency,
        mcc=mcc,
        pm_age_days=int(pm_age_days),
        dist_km=float(dist_km),
        txn1h=int(txn1h),
        txn24h=int(txn24h),
        amt24h=float(amt24h),
        new_token=is_new_token,
    )

    tx_id = f"{txn_id_prefix}{txn_seq:09d}"

    return {
        "TransactionId": tx_id,
        "Amount": float(amount),
        "Country": country,
        "Currency": currency,
        "MerchantId": merchant,
        "CustomerId": customer,
        "DeviceId": device_id,
        "PaymentMethodToken": token,
        "HourOfDay": int(hour),
        "DayOfWeek": int(dow),
        "TransactionType": txn_type,
        "Channel": channel,
        "MerchantCategory": mcc,
        "DeviceType": device_type,
        "IsNewDevice": str(bool(is_new_device)).lower(),
        "IsNewPaymentToken": str(bool(is_new_token)).lower(),
        "IsInternational": str(bool(is_international)).lower(),
        "AccountAgeDays": int(CUSTOMER_ACCOUNT_AGE_DAYS[customer]),
        "CustomerAge": int(CUSTOMER_AGE[customer]),
        "TxnCountLast1h": int(txn1h),
        "TxnCountLast24h": int(txn24h),
        "TotalAmountLast24h": round(float(amt24h), 2),
        "PaymentMethodAgeDays": int(pm_age_days),
        "DistanceFromHomeKm": round(float(dist_km), 2),
        "MccRisk": round(float(mcc_risk), 2),
        "IsHighRiskMcc": str(bool(is_high_risk_mcc)).lower(),
        "IsHighVelocity": str(bool(is_high_velocity)).lower(),
        "IsSuspiciousCombo": str(bool(is_suspicious_combo)).lower(),
        "IsFraud": str(bool(is_fraud_label)).lower(),
        "RiskScore": int(risk_score),
    }


# -------------------------
# Writer (exact fraud rate via shuffled label plan)
# -------------------------
def write_dataset(path: str, n_rows: int, txn_id_prefix: str, start_seq: int, fraud_rate_target: float, seed_offset: int):
    os.makedirs(os.path.dirname(path), exist_ok=True)

    # Per-dataset ring pools to avoid train/test ring ID overlap memorization
    init_fraud_ring_pools(SEED + seed_offset + 9999)

    fraud_target_count = int(round(n_rows * fraud_rate_target))
    legit_target_count = n_rows - fraud_target_count

    labels = [True] * fraud_target_count + [False] * legit_target_count
    rnd = random.Random(SEED + seed_offset)
    rnd.shuffle(labels)

    fraud_count = 0
    with open(path, "w", newline="", encoding="utf-8") as f:
        writer = csv.DictWriter(f, fieldnames=FIELDNAMES)
        writer.writeheader()

        for i in range(n_rows):
            is_fraud_label = labels[i]
            row = sample_row(txn_id_prefix, start_seq + i, is_fraud_label=is_fraud_label)
            if is_fraud_label:
                fraud_count += 1
            writer.writerow(row)

    print(f"Wrote {path} | rows={n_rows:,} fraud={fraud_count:,} ({fraud_count/n_rows:.4%})")


def main():
    write_dataset(
        path=TRAIN_FILE,
        n_rows=TRAIN_ROWS,
        txn_id_prefix="tr_",
        start_seq=1,
        fraud_rate_target=FRAUD_RATE,
        seed_offset=1000
    )

    write_dataset(
        path=TEST_FILE,
        n_rows=TEST_ROWS,
        txn_id_prefix="te_",
        start_seq=1,
        fraud_rate_target=FRAUD_RATE,
        seed_offset=2000
    )

    print("\nDone.")
    print(f"Train: {TRAIN_FILE}")
    print(f"Test : {TEST_FILE}")
    print("\nTuning tips (to reduce 'perfect' results):")
    print(f"- Lower FRAUD_SIGNAL_BUDGET_MAX (currently {FRAUD_SIGNAL_BUDGET_MAX}) to 1 for more overlap.")
    print(f"- Increase FRAUD_CAMOUFLAGE_RATE (currently {FRAUD_CAMOUFLAGE_RATE}) to 0.25 for more FNs.")
    print(f"- Increase LEGIT_LOOKALIKE_RATE (currently {LEGIT_LOOKALIKE_RATE}) to 0.02 for more FPs.")
    print(f"- Reduce FRAUD_RING_SHARE (currently {FRAUD_RING_SHARE}) if rings still make it too easy.")


if __name__ == "__main__":
    main()