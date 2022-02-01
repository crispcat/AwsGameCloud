using Amazon.Lambda.Core;

[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace Serverless
{
    using System;
    using System.Net;
    using System.Linq;
    using System.Threading.Tasks;
    using System.Collections.Generic;
    
    using Amazon;
    using Amazon.GameLift;
    using Amazon.DynamoDBv2;
    using Amazon.GameLift.Model;
    using Amazon.DynamoDBv2.DataModel;
    using Amazon.Lambda.APIGatewayEvents;
    
    using Newtonsoft.Json;

    public class Wizards
    {
        private IDynamoDBContext db;
        private AmazonGameLiftClient gameLiftClient;

        private const string SESSIONS_TABLE_NAME = "SessionsTable";
        private const string META_SERVER_MATCHMAKER = "MetaServerMatchmaker";
        
        private static readonly DynamoDBContextConfig config = new DynamoDBContextConfig
        {
            Conversion = DynamoDBEntryConversion.V2,
            ConsistentRead = true
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
            
            gameLiftClient = new AmazonGameLiftClient();
        }

        public Wizards(IAmazonDynamoDB dbClient, string tableName)
        {
            CreateTypeMapping(typeof(PlayerSession), tableName);
            db = new DynamoDBContext(dbClient, config);
            
            gameLiftClient = new AmazonGameLiftClient();
        }

        public async Task<APIGatewayProxyResponse> MetaSessionWizard(APIGatewayProxyRequest request, ILambdaContext context)
        {
            context.Logger.LogLine($"Matchmaking request from: {request.RequestContext.Identity.SourceIp}\n");
            context.Logger.LogLine($"Auth context: {string.Join(Environment.NewLine, request.RequestContext.Authorizer.Claims)}\n");

            var playerId = request.RequestContext.Authorizer.Claims["cognito:username"];
            
            var fetchedSession = await db.LoadAsync<PlayerSession>(playerId) ?? new PlayerSession
            {
                PlayerId = playerId,
                LastUpdated = DateTime.Now,
            };

            context.Logger.LogLine($"Fetched session: {fetchedSession}\n");
            
            // TODO: uncomment it when matchmaking process calibrated
            // if (fetchedSession.IsActive)
            //     return new APIGatewayProxyResponse { StatusCode = (int) HttpStatusCode.MethodNotAllowed };

            var ticketId = Guid.NewGuid().ToString();
            await gameLiftClient.StartMatchmakingAsync(new StartMatchmakingRequest
            {
                TicketId = ticketId,
                ConfigurationName = Environment.GetEnvironmentVariable(META_SERVER_MATCHMAKER),
                Players = new List<Player> { new Player { PlayerId = Guid.NewGuid().ToString(), Team = "players" }},
            });
            
            context.Logger.LogLine($"Matchmaking started! TicketId: {ticketId}\n");

            // TODO: make sns topic with wss gateway connection to track mm events
            
            MatchmakingTicket ticket = null;
            var ticketIds = new List<string> { ticketId };
            
            bool matchmakingInProgress = true;
            while (matchmakingInProgress)
            {
                await Task.Delay(1000);
                ticket = 
                    (await gameLiftClient.DescribeMatchmakingAsync(new DescribeMatchmakingRequest { TicketIds = ticketIds }))
                    .TicketList
                    .First();

                context.Logger.LogLine($"Matchmaking in progress... TicketId: {ticketId}. Status: {ticket.Status}\n");
                matchmakingInProgress = !MatchmakingIsDone(ticket.Status);
            }
            
            context.Logger.LogLine($"Matchmaking done with result {ticket.Status.Value}\n");
            
            if (ticket.Status != MatchmakingConfigurationStatus.COMPLETED)
                return new APIGatewayProxyResponse { StatusCode = (int) HttpStatusCode.InternalServerError };

            var metaServerSession = fetchedSession.ServerSessions[ServerType.Meta];
            metaServerSession.IsActive = true;
            fetchedSession.LastUpdated = DateTime.Now;
            metaServerSession.SessionId = ticket.GameSessionConnectionInfo.GameSessionArn;
            metaServerSession.Ip = ticket.GameSessionConnectionInfo.IpAddress;
            metaServerSession.Port = ticket.GameSessionConnectionInfo.Port;

            await db.SaveAsync(fetchedSession);

            var response = new APIGatewayProxyResponse
            {
                StatusCode = (int) HttpStatusCode.OK,
                Body = JsonConvert.SerializeObject(fetchedSession),
                Headers = new Dictionary<string, string> {{"Content-Type", "text/plain"}}
            };

            return response;
        }

        private static bool MatchmakingIsDone(MatchmakingConfigurationStatus status)
        {
            return status == MatchmakingConfigurationStatus.FAILED     
                || status == MatchmakingConfigurationStatus.TIMED_OUT
                || status == MatchmakingConfigurationStatus.CANCELLED
                || status == MatchmakingConfigurationStatus.COMPLETED;
        }
    }
}