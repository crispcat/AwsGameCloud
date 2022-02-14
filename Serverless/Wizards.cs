using Amazon.Lambda.Core;

[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace Serverless
{
    using System;
    using System.Net;
    using System.Linq;
    using System.Net.Sockets;
    using System.Threading.Tasks;
    using System.Collections.Generic;
    
    using Amazon;
    using Amazon.SQS;
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
        private MatchmakingMessageConsumer matchmakingMessageConsumer;

        private const string SESSIONS_TABLE_NAME = "SessionsTable";
        private const string META_SERVER_MATCHMAKER = "MetaServerMatchmaker";
        private const string MATCHMAKER_MESSAGES_QUEUE = "MatchmakerEventsQueue";
        
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
            CreateTypeMapping(typeof(PlayerSessionRecord), Environment.GetEnvironmentVariable(SESSIONS_TABLE_NAME));
            db = new DynamoDBContext(new AmazonDynamoDBClient(), config);
            
            gameLiftClient = new AmazonGameLiftClient();
            matchmakingMessageConsumer = new MatchmakingMessageConsumer(Environment.GetEnvironmentVariable(MATCHMAKER_MESSAGES_QUEUE));
        }

        public Wizards(IAmazonDynamoDB dbClient, string tableName)
        {
            CreateTypeMapping(typeof(PlayerSessionRecord), tableName);
            db = new DynamoDBContext(dbClient, config);
            
            gameLiftClient = new AmazonGameLiftClient();
            matchmakingMessageConsumer = new MatchmakingMessageConsumer(Environment.GetEnvironmentVariable(MATCHMAKER_MESSAGES_QUEUE));
        }

        public async Task<APIGatewayProxyResponse> MetaSessionWizard(APIGatewayProxyRequest request, ILambdaContext context)
        {
            context.Logger.LogLine($"Matchmaking request from: {request.RequestContext.Identity.SourceIp}\n");
            context.Logger.LogLine($"Auth context: {string.Join(Environment.NewLine, request.RequestContext.Authorizer.Claims)}\n");

            var playerId = request.RequestContext.Authorizer.Claims["cognito:username"];

            var fetchedSessionRecord = await db.LoadAsync<PlayerSessionRecord>(playerId);
            var fetchedSession = fetchedSessionRecord != null 
                ? fetchedSessionRecord.GetPlayerSession() 
                : new PlayerSession { PlayerId = playerId, LastUpdated = DateTime.UtcNow };

            if (fetchedSession == null)
                return Error();

            context.Logger.LogLine($"Fetched session: {fetchedSession}\n");

            // return last session if session still alive and server is running
            var serverSession = fetchedSession.ServerSessions[ServerType.Meta];
            if (serverSession.IsActive && await TryPingServer(serverSession.Ip, serverSession.Port))
            {
                context.Logger.LogLine("Session still alive. Returning it.");
                // ReSharper disable once PossibleNullReferenceException
                OK(fetchedSessionRecord.data);
            }

            // else start matchmaking
            var ticketId = Guid.NewGuid().ToString();
            var playerMatchId = Guid.NewGuid().ToString();
            await gameLiftClient.StartMatchmakingAsync(new StartMatchmakingRequest
            {
                TicketId = ticketId,
                ConfigurationName = Environment.GetEnvironmentVariable(META_SERVER_MATCHMAKER),
                Players = new List<Player> { new Player { PlayerId = playerMatchId, Team = "players" }},
            });
            
            context.Logger.LogLine($"Matchmaking started! TicketId: {ticketId} PlayerMatchId: {playerMatchId} \n");

            // TODO: make sns topic with wss gateway connection to track mm events
            //ReceiveMatchmakingMessage();
            
            MatchmakingTicket ticket = null;
            var ticketIds = new List<string> { ticketId };
            
            bool matchmakingInProgress = true;
            while (matchmakingInProgress)
            {
                await Task.Delay(1000);
                ticket = 
                    (await gameLiftClient.DescribeMatchmakingAsync(new DescribeMatchmakingRequest { TicketIds = ticketIds }))
                    .TicketList
                    .First(t => t.TicketId == ticketId);

                context.Logger.LogLine($"Matchmaking in progress... TicketId: {ticketId}. Status: {ticket.Status}\n");
                matchmakingInProgress = !MatchmakingIsDone(ticket.Status);
            }
            
            context.Logger.LogLine($"Matchmaking done with result {ticket.Status.Value}\n");
            
            if (ticket.Status != MatchmakingConfigurationStatus.COMPLETED)
                return Error();
            
            var metaServerSession = fetchedSession.ServerSessions[ServerType.Meta];
            metaServerSession.IsActive = true;
            fetchedSession.LastUpdated = DateTime.Now;
            metaServerSession.SessionId = ticket.GameSessionConnectionInfo.MatchedPlayerSessions.First(ps => ps.PlayerId == playerMatchId).PlayerSessionId;
            metaServerSession.Ip = ticket.GameSessionConnectionInfo.IpAddress;
            metaServerSession.Port = ticket.GameSessionConnectionInfo.Port;

            var sessionData = JsonConvert.SerializeObject(fetchedSession);
            await db.SaveAsync(new PlayerSessionRecord(playerId, sessionData));
            
            return OK(sessionData);
        }

        private static APIGatewayProxyResponse OK(string data)
        {
            return new APIGatewayProxyResponse
            {
                StatusCode = (int) HttpStatusCode.OK,
                Body = data,
                Headers = new Dictionary<string, string> {{"Content-Type", "text/plain"}}
            };
        }

        private static APIGatewayProxyResponse Error()
        {
            return new APIGatewayProxyResponse { StatusCode = (int) HttpStatusCode.InternalServerError };
        }

        private static bool MatchmakingIsDone(MatchmakingConfigurationStatus status)
        {
            return status == MatchmakingConfigurationStatus.FAILED     
                || status == MatchmakingConfigurationStatus.TIMED_OUT
                || status == MatchmakingConfigurationStatus.CANCELLED
                || status == MatchmakingConfigurationStatus.COMPLETED;
        }

        private static async Task<bool> TryPingServer(string ip, int port)
        {
            try
            {
                var client = new TcpClient();
                var connect = client.ConnectAsync(ip, port);
                await Task.WhenAny(connect, Task.Delay(3_000));
                var connected = client.Connected;
                
                client.Close();
                client.Dispose();
                
                return connected;
            }
            catch (SocketException)
            {
                return false;
            }
        }
    }
}