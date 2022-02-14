namespace Serverless
{
    using Amazon.SQS;

    public class MatchmakingMessageConsumer
    {
        private AmazonSQSClient sqsClient;
        private string mmSqsQueueUrl;
        
        public MatchmakingMessageConsumer(string mmSqsQueueUrl)
        {
            this.mmSqsQueueUrl = mmSqsQueueUrl;
            sqsClient = new AmazonSQSClient();
        }
        
        
    }
}