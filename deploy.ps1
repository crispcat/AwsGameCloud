cd "Serverless\Serverless\src\Serverless"
dotnet lambda deploy-serverless test-game-infrastructure -sb test-game-infrastructure -tp "ShouldCreateSessionsTable=true;SessionsTableName=SessionsTable"