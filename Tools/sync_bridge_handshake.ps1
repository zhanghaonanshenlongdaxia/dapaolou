# 桥接握手镜像：Temp(桥接实写) -> 项目根(CLI/刷新读取)，每 5 秒
# 用法: powershell -File Tools\sync_bridge_handshake.ps1   (后台常驻)
$proj = "E:\UnityProject\Dapaolou"
$src = Join-Path $proj "Temp\.com-unity-codely.json"
$dst = Join-Path $proj ".com-unity-codely.json"
Write-Host "mirror started: $src -> $dst"
while ($true) {
    if (Test-Path $src) {
        Copy-Item $src $dst -Force -ErrorAction SilentlyContinue
    }
    Start-Sleep -Seconds 5
}
