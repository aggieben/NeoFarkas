dotnet publish --no-self-contained -r linux-x64 -c Release -o publish
ssh ben@chatty.cit.chat rm -rf neofarkas
scp -r publish ben@chatty.cit.chat:neofarkas
ssh ben@chatty.cit.chat chmod u+x neofarkas/NeoFarkas