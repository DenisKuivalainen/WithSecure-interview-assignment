using System.Security.Cryptography;
using Amazon.S3;
using Amazon.S3.Model;

class S3Helpers
{
    private readonly AmazonS3Client s3Client;

    private readonly string FILE_BUCKET;

    public S3Helpers(AmazonS3Client? customClient = null)
    {
        s3Client = customClient ?? new AmazonS3Client();

        FILE_BUCKET = Environment.GetEnvironmentVariable("FILE_BUCKET") ?? "fileBucket";
    }

    private const int CHUNK_SIZE = 5 * 1024 * 1024; // 5MB

    public bool IsFileSmall(long fileSize)
    {
        return fileSize < CHUNK_SIZE;
    }

    public async Task PutObjectAsStream(string key, long fileSize, Stream fileStream, SHA256 sha256)
    {
        using (var hashingStream = new CryptoStream(fileStream, sha256, CryptoStreamMode.Read))
        {
            var request = new PutObjectRequest
            {
                BucketName = FILE_BUCKET,
                Key = key,
                InputStream = hashingStream
            };
            request.Headers["Content-Length"] = fileSize.ToString();

            await s3Client.PutObjectAsync(request);
        }

        sha256.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
    }

    private async Task<string> InitMultipartUpload(string key)
    {
        var initResponse = await s3Client.InitiateMultipartUploadAsync(
            new InitiateMultipartUploadRequest { BucketName = FILE_BUCKET, Key = key }
        );

        return initResponse.UploadId;
    }

    private async Task<PartETag> UploadPart(
        string key,
        string uploadId,
        int partNumber,
        byte[] buffer,
        int bytesRead
    )
    {
        using (var partStream = new MemoryStream(buffer, 0, bytesRead))
        {
            var uploadResponse = await s3Client.UploadPartAsync(
                new UploadPartRequest
                {
                    BucketName = FILE_BUCKET,
                    Key = key,
                    UploadId = uploadId,
                    PartNumber = partNumber,
                    InputStream = partStream
                }
            );

            return new PartETag { PartNumber = partNumber, ETag = uploadResponse.ETag };
        }
    }

    private async Task CompleteMultipartUpload(
        string key,
        string uploadId,
        List<PartETag> partETags
    )
    {
        if (partETags.Count <= 0)
        {
            throw new Exception();
        }

        await s3Client.CompleteMultipartUploadAsync(
            new CompleteMultipartUploadRequest
            {
                BucketName = FILE_BUCKET,
                Key = key,
                UploadId = uploadId,
                PartETags = partETags
            }
        );
    }

    private async Task AbortMultipartUpload(string key, string uploadId)
    {
        await s3Client.AbortMultipartUploadAsync(
            new AbortMultipartUploadRequest
            {
                BucketName = FILE_BUCKET,
                Key = key,
                UploadId = uploadId,
            }
        );
    }

    public async Task MultipartUpload(string key, Stream fileStream, SHA256 sha256)
    {
        string uploadId = await InitMultipartUpload(key);

        try
        {
            List<PartETag> partETags = new List<PartETag>();
            int partNumber = 1;

            while (true)
            {
                byte[] buffer = new byte[CHUNK_SIZE];
                int bytesRead = await fileStream.ReadAsync(buffer, 0, CHUNK_SIZE);

                if (bytesRead == 0)
                    break;

                if (bytesRead < CHUNK_SIZE && partNumber == 1)
                {
                    fileStream.Seek(0, SeekOrigin.Begin);
                    await PutObjectAsStream(key, bytesRead, fileStream, sha256);
                    await AbortMultipartUpload(key, uploadId);
                    return;
                }

                var uploadRes = await UploadPart(key, uploadId, partNumber, buffer, bytesRead);

                partETags.Add(uploadRes);
                sha256.TransformBlock(buffer, 0, bytesRead, buffer, 0);

                partNumber++;
            }

            sha256.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
            await CompleteMultipartUpload(key, uploadId, partETags);
        }
        catch (AmazonS3Exception e)
        {
            await AbortMultipartUpload(key, uploadId);
            throw e; // Not much logic inside try-catch, I allow ignore of stack info change
        }
    }
}
