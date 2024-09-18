Set-Location $PSScriptRoot

Remove-Item -Path "../wwwroot" -Force -Recurse -Confirm:$false

npx tsc
npx tailwindcss -i ./styles/styles.css -o ../wwwroot/css/styles.min.css --minify

Copy-Item -Path "pages/*" -Destination "../wwwroot"
Copy-Item -Path "styles/*" -Destination "../wwwroot/css"