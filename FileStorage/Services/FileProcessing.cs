using System.Security.Cryptography;

namespace FileStorage.Services
{
    class FileProcessing
    {
        public static async Task UploadAsync(Stream fileStream, long fileSize, string fileName)
        {
            var now = DateTime.UtcNow;
            string[] fileNameParts = fileName.Split('.');
            string fileNameWithDate =
                $"{fileNameParts[0]}/{now.ToString("yyyyMMddTHHmmss")}.{string.Join(".", fileNameParts[1..])}";

            using (SHA256 sha256 = SHA256.Create())
            {
                var s3Helpers = new S3Helpers();

                if (s3Helpers.IsFileSmall(fileSize))
                {
                    await s3Helpers.PutObjectAsStream(
                        fileNameWithDate,
                        fileSize,
                        fileStream,
                        sha256
                    );
                }
                else
                {
                    await s3Helpers.MultipartUpload(fileNameWithDate, fileStream, sha256);
                }

                await new DynamoDBHelpers().PutFileHash(
                    fileName,
                    BitConverter.ToString(sha256.Hash!),
                    now
                );
            }
        }
    }
}
