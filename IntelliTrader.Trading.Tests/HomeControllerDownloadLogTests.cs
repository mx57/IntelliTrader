using System;
using System.IO;
using IntelliTrader.Web.Controllers;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace IntelliTrader.Trading.Tests
{
    public class HomeControllerDownloadLogTests : IDisposable
    {
        private readonly string _logDir;

        public HomeControllerDownloadLogTests()
        {
            _logDir = Path.Combine(Directory.GetCurrentDirectory(), "log");
        }

        public void Dispose()
        {
            // Cleanup test log files created during testing
            if (Directory.Exists(_logDir))
            {
                var testFiles = Directory.GetFiles(_logDir, "test-*.txt");
                foreach (var f in testFiles)
                {
                    try { File.Delete(f); } catch { }
                }
            }
        }

        [Fact]
        public void DownloadLog_ReturnsNotFound_WhenNoLogsExist()
        {
            // Arrange
            var controller = new HomeController();
            string nonExistentType = "nonexistent_type_" + Guid.NewGuid().ToString("N");

            // Act
            var result = controller.DownloadLog(nonExistentType);

            // Assert
            var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
            Assert.Equal("Log file not found.", notFoundResult.Value);
        }

        [Fact]
        public void DownloadLog_ReturnsFileStreamResult_WhenLogFileExists()
        {
            // Arrange
            if (!Directory.Exists(_logDir))
            {
                Directory.CreateDirectory(_logDir);
            }

            string testFilePath = Path.Combine(_logDir, "test-unit-general.txt");
            File.WriteAllText(testFilePath, "[INF] Test general log entry\n[INF] Second line");

            var controller = new HomeController();

            try
            {
                // Act
                var result = controller.DownloadLog("general");

                // Assert
                var fileResult = Assert.IsType<FileStreamResult>(result);
                Assert.Equal("text/plain", fileResult.ContentType);
                Assert.NotNull(fileResult.FileDownloadName);
                Assert.EndsWith("-general.txt", fileResult.FileDownloadName);
                Assert.NotNull(fileResult.FileStream);

                using (var reader = new StreamReader(fileResult.FileStream))
                {
                    string content = reader.ReadToEnd();
                    Assert.Contains("Test general log entry", content);
                }
            }
            finally
            {
                if (File.Exists(testFilePath))
                {
                    File.Delete(testFilePath);
                }
            }
        }
    }
}
