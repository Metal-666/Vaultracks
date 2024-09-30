$Version = Read-Host "Please enter the image version (x.y)"

docker buildx build -f ./Vaultracks/Dockerfile -o type=docker -t metal666/vaultracks:latest -t metal666/vaultracks:v$Version --progress=plain .

Read-Host -Prompt "Press Enter to exit"