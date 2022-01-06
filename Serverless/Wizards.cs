using Amazon.Lambda.Core;

[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace Serverless
{
    using System;
    using System.Net;
    using System.Threading.Tasks;
    using System.Collections.Generic;
    
    using Amazon;
    using Amazon.DynamoDBv2;
    using Amazon.DynamoDBv2.DataModel;
    using Amazon.Lambda.APIGatewayEvents;
    
    using Newtonsoft.Json;

    public class Wizards
    {
        private IDynamoDBContext db;

        private const string SESSIONS_TABLE_NAME = "SessionsTable";
        
        private static readonly DynamoDBContextConfig config = new DynamoDBContextConfig
        {
            Conversion = DynamoDBEntryConversion.V2
        };

        private static void CreateTypeMapping(Type t, string tableName)
        {
            if (!string.IsNullOrEmpty(tableName))
                AWSConfigsDynamoDB.Context.TypeMappings[t] = new Amazon.Util.TypeMapping(t, tableName);
        }
        
        public Wizards()
        {
            CreateTypeMapping(typeof(PlayerSession), Environment.GetEnvironmentVariable(SESSIONS_TABLE_NAME));
            db = new DynamoDBContext(new AmazonDynamoDBClient(), config);
        }

        public Wizards(IAmazonDynamoDB dbClient, string tableName)
        {
            CreateTypeMapping(typeof(PlayerSession), tableName);
            db = new DynamoDBContext(dbClient, config);
        }

        public async Task<APIGatewayProxyResponse> MetaSessionWizard(APIGatewayProxyRequest request, ILambdaContext context)
        {
            context.Logger.LogLine($"Request from: {request.RequestContext.Identity.SourceIp}\n");

            var session = new PlayerSession
            {
                PlayerId = Guid.NewGuid().ToString(),
                LastUpdated = DateTime.Now,
            };

            await db.SaveAsync(session);
            var fetchedSession = await db.LoadAsync<PlayerSession>(session.PlayerId);

            var response = new APIGatewayProxyResponse
            {
                StatusCode = (int) HttpStatusCode.OK,
                Body = JsonConvert.SerializeObject(fetchedSession),
                Headers = new Dictionary<string, string> {{"Content-Type", "text/plain"}}
            };

            return response;
        }
    }
}