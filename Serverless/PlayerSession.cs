namespace Serverless
{
    using System;
    using System.Linq;
    using Newtonsoft.Json;
    using System.Collections.Generic;
    using Amazon.DynamoDBv2.DataModel;

    public class PlayerSessionRecord
    {
        [DynamoDBHashKey] public string PlayerId { get; set; }
        
        public string data { get; set; }
        
        public PlayerSessionRecord()
        {
        }
        
        public PlayerSessionRecord(string playerId, string data)
        {
            PlayerId = playerId;
            this.data = data;
        }
        
        public PlayerSessionRecord(PlayerSession ps)
        {
            PlayerId = ps.PlayerId;
            data = JsonConvert.SerializeObject(ps);
        }

        public PlayerSession GetPlayerSession()
        {
            return JsonConvert.DeserializeObject<PlayerSession>(data);
        }
    }
    
    public class PlayerSession
    {
        public string PlayerId { get; set; }

        public DateTime LastUpdated { get; set; }

        public Dictionary<ServerType, ServerSession> ServerSessions { get; set; }

        [JsonConstructor]
        public PlayerSession()
        {
            ServerSessions = new Dictionary<ServerType, ServerSession>
            {
                { ServerType.Meta, new ServerSession() },
                { ServerType.Battle, new ServerSession() },
                { ServerType.Lobby, new ServerSession() }
            };
        }

        public override string ToString()
        {
            return $"{nameof(PlayerId)}: {PlayerId}, " 
                 + $"{nameof(LastUpdated)}: {LastUpdated}, "
                 + $"{nameof(ServerSessions)}: {ServerSessions.PrintCollection()}";
        }
    }
    
    public enum ServerType
    {
        Meta,
        Battle,
        Lobby
    }
    
    public class ServerSession
    {
        public string SessionId { get; set; }

        public bool IsActive { get; set; }

        public string Ip { get; set; }

        public int Port { get; set; }
        
        public int DebuggerPort { get; set; }

        public override string ToString()
        {
            return $"{nameof(SessionId)}: {SessionId}"
                   + $"{nameof(IsActive)}: {IsActive}"
                   + $"{nameof(Ip)}: {Ip}"
                   + $"{nameof(Port)}: {Port}";
        }
    }
    
    public static class Ext
    {
        public static string PrintCollection<T>(this IEnumerable<T> collection, string delimiter = ", ")
        {
            if (collection == null) return "empty collection";
            return string.Join(delimiter, collection.Select(val => val?.ToString()).ToArray());
        }
    }
}