namespace Serverless.Tests
{
    using Xunit;
    using System;
    using Amazon;
    using Xunit.Sdk;
    using System.Threading;
    using Amazon.DynamoDBv2;
    using Amazon.DynamoDBv2.Model;
    using System.Threading.Tasks;
    using System.Collections.Generic;
    using System.Net;
    using Amazon.Lambda.TestUtilities;
    using Amazon.Lambda.APIGatewayEvents;
    using Xunit.Abstractions;

    public sealed class WizardsTests : IDisposable
    {
        private readonly string tableName;
        private readonly IAmazonDynamoDB dbClient;
        
        private readonly ITestOutputHelper sink;

        public WizardsTests(ITestOutputHelper sink)
        {
            this.sink = sink;

            tableName = "BlueprintBaseName-SessionTable-" + DateTime.Now.Ticks;
            dbClient = new AmazonDynamoDBClient(RegionEndpoint.USWest2);

            SetupTableAsync().Wait();
        }

        [Fact]
        public async void TestWizards()
        {
            var wizards = new Wizards(dbClient, tableName);

            var request = new APIGatewayProxyRequest
            {
                RequestContext = new APIGatewayProxyRequest.ProxyRequestContext()
                {
                    Identity = new APIGatewayProxyRequest.RequestIdentity()
                    {
                        SourceIp = IPAddress.Loopback.ToString()
                    }
                }
            };
            
            var context = new TestLambdaContext();
            var response = await wizards.MetaSessionWizard(request, context);
            
            Assert.Equal(200, response.StatusCode);
            sink.WriteLine(response.Body);
        }
        
        /// <summary>
        /// Create the DynamoDB table for testing. This table is deleted as part of the object dispose method.
        /// </summary>
        /// <returns></returns>
        private async Task SetupTableAsync()
        {
            var request = new CreateTableRequest
            {
                TableName = tableName,
                KeySchema = new List<KeySchemaElement>
                {
                    new KeySchemaElement
                    {
                        AttributeName = nameof(PlayerSession.PlayerId),
                        KeyType = KeyType.HASH,
                    }
                },
                AttributeDefinitions = new List<AttributeDefinition>
                {
                    new AttributeDefinition
                    {
                        AttributeName = nameof(PlayerSession.PlayerId),
                        AttributeType = ScalarAttributeType.S
                    }
                },
                BillingMode = BillingMode.PAY_PER_REQUEST
            };

            await dbClient.CreateTableAsync(request);

            var describeRequest = new DescribeTableRequest { TableName = tableName };
            DescribeTableResponse response = null;
            do
            {
                Thread.Sleep(1000);
                response = await dbClient.DescribeTableAsync(describeRequest);
            } while (response.Table.TableStatus != TableStatus.ACTIVE);
        }
        
        #region IDisposable Support

        private bool disposedValue = false; // To detect redundant calls

        private void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    dbClient.DeleteTableAsync(tableName).Wait();
                    dbClient.Dispose();
                }

                disposedValue = true;
            }
        }

        public void Dispose()
        {
            Dispose(true);
        }

        #endregion
    }
}