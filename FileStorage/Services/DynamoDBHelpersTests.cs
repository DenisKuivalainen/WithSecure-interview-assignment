using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Moq;
using Xunit;

namespace FileStorage.Tests
{
    public class DynamoDBHelpersTests
    {
        private readonly Mock<AmazonDynamoDBClient> _mockDynamoDbClient;
        private readonly DynamoDBHelpers _dynamoDBHelpers;

        public DynamoDBHelpersTests()
        {
            Environment.SetEnvironmentVariable("HASHES_TABLE", "customTableName");

            _mockDynamoDbClient = new Mock<AmazonDynamoDBClient>();
            _dynamoDBHelpers = new DynamoDBHelpers(_mockDynamoDbClient.Object);
        }

        private bool AreEqualItems(
            Dictionary<string, AttributeValue> actual,
            Dictionary<string, AttributeValue> expected
        )
        {
            foreach (var key in expected.Keys)
            {
                if (!actual.ContainsKey(key) || actual[key].S != expected[key].S)
                {
                    return false;
                }
            }
            return true;
        }

        [Fact]
        public async Task PutFileHash_RecordCreated()
        {
            var fileName = "test.txt";
            var hash = "asdasdasd";
            var uploadedAt = DateTime.UtcNow;

            var expectedItem = new Dictionary<string, AttributeValue>
            {
                {
                    "Filename",
                    new AttributeValue { S = fileName }
                },
                {
                    "Hash",
                    new AttributeValue { S = hash }
                },
                {
                    "UploadedAt",
                    new AttributeValue { S = uploadedAt.ToString("o") }
                }
            };

            await _dynamoDBHelpers.PutFileHash(fileName, hash, uploadedAt);

            _mockDynamoDbClient.Verify(
                client =>
                    client.PutItemAsync(
                        It.Is<PutItemRequest>(
                            request =>
                                request.TableName == "customTableName"
                                && AreEqualItems(request.Item, expectedItem)
                        ),
                        default
                    ),
                Times.Once
            );
        }
    }
}
