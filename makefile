deploy:
	dotnet publish
	-@sudo rm /usr/local/bin/RV_Fabrication
	sudo cp bin/Release/net8.0/linux-x64/publish/RV_Fabrication /usr/local/bin/
