Set-Location $PSScriptRoot

Remove-Item -Path "../wwwroot" -Force -Recurse -Confirm:$false

npx tailwindcss -o ../wwwroot/css/styles.min.css --minify

Copy-Item -Path "pages/*" -Destination "../wwwroot"