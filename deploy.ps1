$gameName=$args[0]
$version=$args[1]
$region=$args[2]
$deployGameServers=$args[3]
$serverBuildPath=$args[4]

if ($serverBuildPath -ne $null)
{
	Remove-Item "$($serverBuildPath)\build*.zip"
	
	$compress = @{
		Path = "$($serverBuildPath)\*"
		DestinationPath = "$($serverBuildPath)\build_$($version).zip"
	}

	Compress-Archive @compress -Force
	aws s3api create-bucket --bucket $gameName --region us-east-1 --create-bucket-configuration "LocationConstraint=$($region)"
	
	Write-Host "Uploading builds..." -ForegroundColor green
	aws s3api put-object --bucket $gameName --key "meta_server_build_$($version).zip" --body "$($serverBuildPath)\build_$($version).zip"
}

Write-Host "Creating $($gameName) cloud... Be patient! This can take a long time." -ForegroundColor green
Write-Host "Track fleet progresses. They are a lazy ones: https://$($region).console.aws.amazon.com/gamelift/home?region=$($region)#/" -ForegroundColor yellow

cd "Serverless\"
dotnet lambda deploy-serverless $gameName -sb $gameName -tp "GameName=$($gameName);S3GameServerBucket=$($gameName);GameServerVersion=$($version);DeployGameServers=$($deployGameServers)"
cd ..