using Moq;
using TransactionService.Models;
using TransactionService.Services;

namespace TransactionService.Tests
{
    /// <summary>
    /// Unit tests for TransactionAppService business logic.
    /// </summary>
    public class TransactionAppServiceTests
    {
        /// <summary>
        /// Verifies that CreateAsync normalizes data and calls the repository.
        /// </summary>
        [Fact]
        public async Task CreateAsync_NormalizesAndPersistsTransaction()
        {
            // Arrange
            var repoMock = new Mock<ITransactionRepository>();

            var request = new CreateTransactionRequest
            {
                Amount = 100.50m,
                Currency = "aud", // lower case on purpose
                MerchantId = "merchant_123",
                CustomerId = "customer_456",
                PaymentMethodToken = "pm_tok_abc123",
                DeviceId = "device_xyz",
                Country = "au", // lower case on purpose
                Timestamp = null // let the service set it
            };

            // We capture the Transaction that the service passes to the repository.
            Transaction? capturedTransaction = null;

            repoMock
                .Setup(r => r.AddAsync(It.IsAny<Transaction>()))
                .ReturnsAsync((Transaction t) =>
                {
                    capturedTransaction = t;
                    // Simulate that EF would have saved it and kept values.
                    return t;
                });

            var service = new TransactionAppService(repoMock.Object);

            // Act
            var response = await service.CreateAsync(request);

            // Assert
            Assert.NotNull(response);
            Assert.Equal(100.50m, response.Amount);
            Assert.Equal("AUD", response.Currency);   // normalized
            Assert.Equal("AU", response.Country);     // normalized
            Assert.Equal("merchant_123", response.MerchantId);
            Assert.Equal("customer_456", response.CustomerId);
            Assert.Equal("pm_tok_abc123", response.PaymentMethodToken);
            Assert.Equal("device_xyz", response.DeviceId);
            Assert.NotEqual(default, response.Timestamp); // service should set a timestamp

            // Ensure we actually called the repository
            repoMock.Verify(r => r.AddAsync(It.IsAny<Transaction>()), Times.Once);

            // And the transaction passed to the repo was also normalized
            Assert.NotNull(capturedTransaction);
            Assert.Equal("AUD", capturedTransaction!.Currency);
            Assert.Equal("AU", capturedTransaction.Country);
        }

        /// <summary>
        /// Verifies that GetByIdAsync returns null when the repository returns null.
        /// </summary>
        [Fact]
        public async Task GetByIdAsync_ReturnsNull_WhenNotFound()
        {
            // Arrange
            var repoMock = new Mock<ITransactionRepository>();

            repoMock
                .Setup(r => r.GetByIdAsync(It.IsAny<Guid>()))
                .ReturnsAsync((Transaction?)null);

            var service = new TransactionAppService(repoMock.Object);
            var id = Guid.NewGuid();

            // Act
            var result = await service.GetByIdAsync(id);

            // Assert
            Assert.Null(result);
            repoMock.Verify(r => r.GetByIdAsync(id), Times.Once);
        }

        /// <summary>
        /// Verifies that GetByIdAsync maps the domain entity to a response DTO correctly.
        /// </summary>
        [Fact]
        public async Task GetByIdAsync_ReturnsMappedResponse_WhenFound()
        {
            // Arrange
            var repoMock = new Mock<ITransactionRepository>();

            var txId = Guid.NewGuid();
            var stored = new Transaction
            {
                Id = txId,
                Amount = 250m,
                Currency = "AUD",
                MerchantId = "merchant_xyz",
                CustomerId = "customer_789",
                PaymentMethodToken = "pm_tok_xyz789",
                DeviceId = "device_123",
                Country = "AU",
                Timestamp = DateTimeOffset.UtcNow
            };

            repoMock
                .Setup(r => r.GetByIdAsync(txId))
                .ReturnsAsync(stored);

            var service = new TransactionAppService(repoMock.Object);

            // Act
            var result = await service.GetByIdAsync(txId);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(txId, result!.Id);
            Assert.Equal(250m, result.Amount);
            Assert.Equal("AUD", result.Currency);
            Assert.Equal("merchant_xyz", result.MerchantId);
            Assert.Equal("customer_789", result.CustomerId);
            Assert.Equal("pm_tok_xyz789", result.PaymentMethodToken);
            Assert.Equal("device_123", result.DeviceId);
            Assert.Equal("AU", result.Country);

            repoMock.Verify(r => r.GetByIdAsync(txId), Times.Once);
        }
    }
}