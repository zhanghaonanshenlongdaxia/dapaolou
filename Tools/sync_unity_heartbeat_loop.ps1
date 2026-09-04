# Codely 心跳垫片后台同步器（带日志版）：每 10 秒把 Temp/.com-unity-codely.json
# （1.0.81 桥接真实心跳）镜像成项目根 .com-unity-codely.json（旧 CLI 认的格式+新鲜 last_heartbeat）。
# Temp 消失（Unity 域重载/关闭）超 900 秒自动退出（域重载+重编译可能持续数分钟）。日志：Tools\sync_heartbeat.log
$root = "E:\UnityProject\Dapaolou"
$src = Join-Path $root "Temp\.com-unity-codely.json"
$dst = Join-Path $root ".com-unity-codely.json"
$log = Join-Path $root "Tools\sync_heartbeat.log"
function Log($m) { Add-Content -Path $log -Value "$(Get-Date -Format 'HH:mm:ss') $m" }
Log "=== sync loop started (pid $PID) ==="
$absentSince = $null
while ($true) {
    try {
        if (Test-Path $src) {
            $absentSince = $null
            $srcJson = Get-Content $src -Raw | ConvertFrom-Json
            if ($srcJson.reason -eq "ready" -and $srcJson.unity_port -gt 0) {
                $now = (Get-Date).ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'")
                $epoch = [DateTimeOffset]::UtcNow.ToUnixTimeSeconds()
                $shim = [ordered]@{
                    unity_port     = $srcJson.unity_port
                    unity_host     = "localhost"
                    stream_port    = $srcJson.unity_port
                    created_date   = $srcJson.last_updated
                    project_path   = $srcJson.project_path
                    reloading      = $srcJson.reloading
                    reason         = $srcJson.reason
                    seq            = $epoch
                    last_heartbeat = $now
                } | ConvertTo-Json
                Set-Content -Path $dst -Value $shim -Encoding UTF8
                Log "wrote port=$($srcJson.unity_port)"
            } else {
                Log "bridge not ready: reason=$($srcJson.reason) port=$($srcJson.unity_port)"
            }
        } else {
            if ($null -eq $absentSince) { $absentSince = Get-Date; Log "src missing" }
            if (((Get-Date) - $absentSince).TotalSeconds -gt 900) { Log "src gone 900s, exit"; break }
        }
    } catch {
        Log "ERROR: $($_.Exception.Message)"
    }
    Start-Sleep -Seconds 10
}
