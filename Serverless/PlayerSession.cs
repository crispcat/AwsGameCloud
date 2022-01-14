namespace Serverless
{
    using System;
    using Amazon.DynamoDBv2.DataModel;

    public class PlayerSession
    {
        public const string KEY_NAME = "PlayerId";
        
        [DynamoDBHashKey] public string PlayerId { get; set; }
        
        public DateTime LastUpdated { get; set; }

        public bool IsActive { get; set; }

        public string MetaGameSessionArn { get; set; }

        public string MetaServerIp { get; set; }

        public int MetaServerPort { get; set; }
    }
}