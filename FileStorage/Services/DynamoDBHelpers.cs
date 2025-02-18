using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;

class DynamoDBHelpers
{
    private readonly AmazonDynamoDBClient dynamoDbClient;

    private readonly string HASHES_TABLE;

    public DynamoDBHelpers(AmazonDynamoDBClient? customClient = null)
    {
        dynamoDbClient = customClient ?? new AmazonDynamoDBClient();

        HASHES_TABLE = Environment.GetEnvironmentVariable("HASHES_TABLE") ?? "hashesTable";
    }

    #region DB access methods
    private async Task Put(string tableName, Dictionary<string, AttributeValue> item)
    {
        await dynamoDbClient.PutItemAsync(
            new PutItemRequest { TableName = tableName, Item = item }
        );
    }
    #endregion

    public async Task PutFileHash(string fileName, string hash, DateTime uploadedAt)
    {
        await Put(
            HASHES_TABLE,
            new Dictionary<string, AttributeValue>
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
            }
        );
    }
}
