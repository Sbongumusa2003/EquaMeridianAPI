using EquaMeridian.DTOs.Reviews;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Moq;
using Xunit;
using System.Net;
using System.Security.Claims;
using static EquaMeridian.DTOs.Reviews.ReviewDto;

namespace EquaMeridian.Tests 
{
    public class ReviewsControllerTests
    {
        private readonly Mock<IReviewRepository> _mockRepo;
        private readonly Mock<IAuditService> _mockAudit;
        private readonly Mock<IEmailService> _mockEmail;
        private readonly Mock<IConfiguration> _mockConfig;
        private readonly Mock<INotificationRepository> _mockNotifications;
        private readonly ReviewsController _controller;

        public ReviewsControllerTests()
        {
            _mockRepo = new Mock<IReviewRepository>();
            _mockAudit = new Mock<IAuditService>();
            _mockEmail = new Mock<IEmailService>();
            _mockConfig = new Mock<IConfiguration>();
            _mockNotifications = new Mock<INotificationRepository>();

            _controller = new ReviewsController(
                _mockRepo.Object,
                _mockAudit.Object,
                _mockEmail.Object,
                _mockConfig.Object,
                _mockNotifications.Object
            );

            var userClaims = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
            new Claim(ClaimTypes.NameIdentifier, "42")
        }, "mock-auth"));

            var httpContext = new DefaultHttpContext { User = userClaims };
            httpContext.Connection.RemoteIpAddress = IPAddress.Parse("127.0.0.1");

            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = httpContext
            };
        }

        [Fact]
        public async Task Create_ReturnsOk_WhenReviewIsSuccessfullyCreated()
        {
            var dto = new CreateReviewDto { BookingID = 100, Title = "Great service", OverallRating = 5 };

            var ReviewTest = new ReviewActionResult
            {
                Success = true,
                SupplierEmail = "supplier@example.com",
                SupplierName = "John Doe",
                Machinery = "Excavator X3",
                SupplierID = 99,
                Review = new ReviewDto
                {
                    ReviewID = 500,
                    MachineryID = 10,
                    OverallRating = 5,
                    Title = "Great service"
                }
            };

            _mockRepo.Setup(repo => repo.CreateAsync(42, dto))
                     .ReturnsAsync(ReviewTest);

            var response = await _controller.Create(dto);

           
            var okResult = Assert.IsType<OkObjectResult>(response);
            Assert.NotNull(okResult.Value);


            _mockAudit.Verify(a => a.LogAsync(42, "Review_Created", It.IsAny<string>(), null, 10, null, "127.0.0.1", It.IsAny<string>()), Times.Once);
            _mockEmail.Verify(e => e.SendReviewSubmittedEmailAsync("supplier@example.com", "John Doe", 100, "Excavator X3", 5, "Great service"), Times.Once);
            _mockNotifications.Verify(n => n.CreateAsync(99, "ReviewSubmitted", "New Review", It.IsAny<string>(), "Listing", 10, false), Times.Once);
        }
    }

};


