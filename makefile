deploy:
	dotnet publish
	-sudo rm /usr/local/bin/RV_Bozoer
	sudo cp bin/Release/net8.0/linux-x64/publish/RV_Bozoer /usr/local/bin/
