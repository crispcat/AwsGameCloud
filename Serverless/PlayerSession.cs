namespace Serverless
{
    using System;
    using Amazon.DynamoDBv2.DataModel;

    public class PlayerSession
    {
        public const string KEY_NAME = "PlayerId";
        
        [DynamoDBHashKey] public string PlayerId { get; set; }
        
        public string MetaFleetId { get; set; }
        public string MetaServerId { get; set; }
        public DateTime LastUpdated { get; set; }
    }
}