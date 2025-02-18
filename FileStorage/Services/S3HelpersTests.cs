using Moq;
using System.Security.Cryptography;
using Amazon.S3;
using Amazon.S3.Model;
using Xunit;

namespace FileStorage.Tests
{
    public class S3HelpersTests
    {
        private readonly Mock<AmazonS3Client> _mockS3Client;
        private readonly S3Helpers _s3Helpers;

        private static string key = "test.txt";

        public S3HelpersTests()
        {
            _mockS3Client = new Mock<AmazonS3Client>();
            _s3Helpers = new S3Helpers(_mockS3Client.Object);

            _mockS3Client.Setup(s3 => s3.PutObjectAsync(It.IsAny<PutObjectRequest>(), default));
            _mockS3Client
                .Setup(
                    s3 =>
                        s3.InitiateMultipartUploadAsync(
                            It.IsAny<InitiateMultipartUploadRequest>(),
                            default
                        )
                )
                .ReturnsAsync(new InitiateMultipartUploadResponse { UploadId = "testUploadId" });
            _mockS3Client.Setup(
                s3 =>
                    s3.CompleteMultipartUploadAsync(
                        It.IsAny<CompleteMultipartUploadRequest>(),
                        default
                    )
            );
            _mockS3Client.Setup(
                s3 => s3.AbortMultipartUploadAsync(It.IsAny<AbortMultipartUploadRequest>(), default)
            );
        }

        [Fact]
        public void IsFileSmall_SmallFile()
        {
            var fileSize = 516 * 1024;

            Assert.True(_s3Helpers.IsFileSmall(fileSize));
        }

        [Fact]
        public void IsFileSmall_LargeFile()
        {
            var fileSize = 10 * 1024 * 1024;

            Assert.False(_s3Helpers.IsFileSmall(fileSize));
        }

        [Fact]
        public async Task PutObjectAsStream_SuccessfulUpload()
        {
            var fileSize = 256 * 1024;
            var fileStream = new MemoryStream(new byte[fileSize]);
            var sha256 = SHA256.Create();

            await _s3Helpers.PutObjectAsStream(key, fileSize, fileStream, sha256);

            _mockS3Client.Verify(
                s3 =>
                    s3.PutObjectAsync(
                        It.Is<PutObjectRequest>(
                            request =>
                                request.BucketName == "fileBucket"
                                && request.Key == key
                                && request.Headers["Content-Length"] == fileSize.ToString()
                        ),
                        default
                    ),
                Times.Once
            );
        }

        [Fact]
        public async Task MultipartUpload_SuccessfulUpload()
        {
            var fileStream = new MemoryStream(new byte[100 * 1024 * 1024]);
            var sha256 = SHA256.Create();

            _mockS3Client
                .Setup(s3 => s3.UploadPartAsync(It.IsAny<UploadPartRequest>(), default))
                .ReturnsAsync(new UploadPartResponse { ETag = "etag" });

            await _s3Helpers.MultipartUpload(key, fileStream, sha256);

            _mockS3Client.Verify(
                s3 =>
                    s3.InitiateMultipartUploadAsync(
                        It.IsAny<InitiateMultipartUploadRequest>(),
                        default
                    ),
                Times.Once
            );
            _mockS3Client.Verify(
                s3 => s3.UploadPartAsync(It.IsAny<UploadPartRequest>(), default),
                Times.Exactly(20)
            );
            _mockS3Client.Verify(
                s3 =>
                    s3.CompleteMultipartUploadAsync(
                        It.IsAny<CompleteMultipartUploadRequest>(),
                        default
                    ),
                Times.Once
            );
        }

        [Fact]
        public async Task MultipartUpload_FailedUpload()
        {
            var fileStream = new MemoryStream(new byte[10 * 1024 * 1024]); // 10MB
            var sha256 = SHA256.Create();

            _mockS3Client
                .Setup(s3 => s3.UploadPartAsync(It.IsAny<UploadPartRequest>(), default))
                .ThrowsAsync(new AmazonS3Exception("Upload failed."));

            await Assert.ThrowsAsync<AmazonS3Exception>(
                () => _s3Helpers.MultipartUpload(key, fileStream, sha256)
            );

            _mockS3Client.Verify(
                s3 =>
                    s3.AbortMultipartUploadAsync(It.IsAny<AbortMultipartUploadRequest>(), default),
                Times.Once
            );
        }

        [Fact]
        public async Task MultipartUpload_SmallFile()
        {
            var fileStream = new MemoryStream(new byte[128 * 1024]);
            var sha256 = SHA256.Create();

            await _s3Helpers.MultipartUpload(key, fileStream, sha256);

            _mockS3Client.Verify(
                s3 => s3.PutObjectAsync(It.IsAny<PutObjectRequest>(), default),
                Times.Once
            );
            _mockS3Client.Verify(
                s3 =>
                    s3.AbortMultipartUploadAsync(It.IsAny<AbortMultipartUploadRequest>(), default),
                Times.Once
            );
        }
    }
}
