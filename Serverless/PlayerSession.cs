namespace Serverless
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    public enum ServerType
    {
        Meta,
        Battle,
        Lobby
    }

    public class PlayerSession
    {
        public class ServerSession
        {
            public string SessionId { get; set; }

            public bool IsActive { get; set; }

            public string Ip { get; set; }

            public int Port { get; set; }

            #if !RELEASE
            public int DebuggerPort { get; set; }
            #endif

            public override string ToString()
            {
                return $"{nameof(SessionId)}: {SessionId}"
                     + $"{nameof(IsActive)}: {IsActive}"
                     + $"{nameof(Ip)}: {Ip}"
                     + $"{nameof(Port)}: {Port}";
            }
        }

        public string PlayerId { get; set; }

        public DateTime LastUpdated { get; set; }

        public Dictionary<ServerType, ServerSession> ServerSessions { get; set; }

        public PlayerSession()
        {
            ServerSessions = new Dictionary<ServerType, ServerSession>
            {
                {ServerType.Meta, new ServerSession()},
                {ServerType.Battle, new ServerSession()},
                {ServerType.Lobby, new ServerSession()}
            };
        }

        public override string ToString()
        {
            return $"{nameof(PlayerId)}: {PlayerId}, " 
                 + $"{nameof(LastUpdated)}: {LastUpdated}, "
                 + $"{nameof(ServerSessions)}: {ServerSessions.PrintCollection()}";
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