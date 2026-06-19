$ErrorActionPreference = "Stop"
$HOST = "http://localhost:5185"

Write-Host "========================================"
Write-Host "  Aquarius 娴嬭瘯鏁版嵁鐢熸垚"
Write-Host "========================================"
Write-Host ""

# 鈹€鈹€ Helpers 鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€
function Invoke-Api($method, $path, $body, $token) {
    $headers = @{ "Content-Type" = "application/json" }
    if ($token) { $headers["Authorization"] = "Bearer $token" }
    $json = $body | ConvertTo-Json -Compress
    $uri = "$HOST$path"
    try {
        if ($method -eq "POST") {
            return Invoke-RestMethod -Uri $uri -Method Post -Headers $headers -Body $json -ContentType "application/json; charset=utf-8"
        }
    } catch {
        Write-Host "  WARN: $_" -ForegroundColor Yellow
        return $null
    }
}

# 鈹€鈹€ 1. Register 鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€
Write-Host "[1/5] 娉ㄥ唽..." -ForegroundColor Cyan
$r1 = Invoke-Api POST "/api/auth/register" @{username="admin"; email="admin@test.com"; password="123456"; confirmPassword="123456"}
Write-Host "  admin 娉ㄥ唽瀹屾垚 (isAdmin=$($r1.isAdmin))"
$r2 = Invoke-Api POST "/api/auth/register" @{username="user1"; email="user1@test.com"; password="123456"; confirmPassword="123456"}
Write-Host "  user1 娉ㄥ唽瀹屾垚 (isAdmin=$($r2.isAdmin))"

# 鈹€鈹€ 2. Login 鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€
Write-Host "[2/5] 鐧诲綍..." -ForegroundColor Cyan
$l1 = Invoke-Api POST "/api/auth/login" @{login="admin"; password="123456"}
$ADM = $l1.token
Write-Host "  ADMIN_TOKEN=$ADM"
$l2 = Invoke-Api POST "/api/auth/login" @{login="user1"; password="123456"}
$U1 = $l2.token
Write-Host "  USER1_TOKEN=$U1"

# 鈹€鈹€ 3. Bottles 鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€
Write-Host "[3/5] 鎶曠摱..." -ForegroundColor Cyan
Invoke-Api POST "/api/bottles" @{Content="浠婂ぉ鍦ㄥ湴閾佷笂鐪嬪埌涓€涓汉鎹х潃涓€鏈焊璐ㄤ功鍦ㄨ锛屽湪杩欎釜浜轰汉浣庡ご鍒锋墜鏈虹殑鏃朵唬锛岀獊鐒惰寰楀ソ鐝嶈吹銆傛垜涔熻鎶婂簥澶撮偅鏈惤鐏扮殑涔︾炕寮€浜嗐€?; AuthorName="鍦伴搧鏃呬汉"} $ADM | Out-Null
Write-Host "  鐡跺瓙1 (admin)"
Invoke-Api POST "/api/bottles" @{Content="鏈€杩戞€诲湪娣卞闂嚜宸憋細鎴戝埌搴曟兂瑕佷粈涔堟牱鐨勭敓娲伙紵鏄畨绋崇殑鏈濅節鏅氫簲锛岃繕鏄啋闄╃殑鑷敱鑱屼笟锛熸湅鍙嬩滑閮借鎴戝お鐖辨姌鑵句簡锛屽彲涓嶆姌鑵炬€庝箞鐭ラ亾鍝潯璺鍛€?; AuthorName="娣卞鎬濊€冭€?} $ADM | Out-Null
Write-Host "  鐡跺瓙2 (admin)"
Invoke-Api POST "/api/bottles" @{Content="鍏讳簡涓ゅ勾鐨勭尗浠婂ぉ璺冲埌鎴戣吙涓婄潯鐫€浜嗭紝鍙戝嚭鍜曞櫆鍜曞櫆鐨勫０闊炽€傜獊鐒惰寰楋紝琚竴鍙皬鐢熷懡淇′换鐨勬劅瑙夛紝姣斾粈涔堥兘娌绘剤銆傚笇鏈涗綘浠婂ぉ涔熸湁璁╀綘蹇冭蒋鐨勫皬鐬棿銆?} $ADM | Out-Null
Write-Host "  鐡跺瓙3 (admin,鍖垮悕)"
Invoke-Api POST "/api/bottles" @{Content="绗竴娆″皾璇曞仛绾㈢儳鑲夛紝缁撴灉鎶婂帹鎴挎悶寰楀儚鐏鹃毦鐜板満銆傝櫧鐒跺崠鐩镐笉濂斤紝浣嗗懗閬撶珶鐒惰繕琛岋紒鐢熸椿澶ф灏辨槸杩欐牱鍚р€斺€旂湅鐫€涔变竷鍏碂锛屽皾璧锋潵鍗存湁婊嬫湁鍛炽€?; AuthorName="鍘ㄦ埧鍐掗櫓瀹?} $U1 | Out-Null
Write-Host "  鐡跺瓙4 (user1)"
Invoke-Api POST "/api/bottles" @{Content="姣曚笟涓夊勾浜嗭紝浠婂ぉ璺繃姣嶆牎锛屽彂鐜版牎闂ㄥ彛閭ｅ濂惰尪搴楄繕寮€鐫€銆傞樋濮ㄥ眳鐒惰繕璁板緱鎴戯紝闂垜杩樻槸鑰佹牱瀛愶紵涓€鐬棿鐪肩湺灏辩儹浜嗐€傛湁浜涘湴鏂癸紝姘歌繙鏄綘鐨勯伩椋庢腐銆?; AuthorName="姣曚笟娓稿瓙"} $U1 | Out-Null
Write-Host "  鐡跺瓙5 (user1)"

# 鈹€鈹€ 4. Comments 鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€
Write-Host "[4/5] 璇勮涓庡洖澶?.." -ForegroundColor Cyan
Invoke-Api POST "/api/bottles/1/comments" @{Content="娣辨湁鍚屾劅锛佹垜鏈€杩戜篃寮€濮嬮噸鏂扮湅绾歌川涔︿簡锛岄偅绉嶇炕椤电殑鎰熻鍜屽ⅷ棣欐槸鐢靛瓙灞忓箷姘歌繙鏇夸唬涓嶄簡鐨勩€?} $ADM | Out-Null
Write-Host "  璇?: admin > 鐡跺瓙1"
Invoke-Api POST "/api/bottles/1/comments" @{Content="鑳介棶闂綘鐪嬬殑浠€涔堜功鍚楋紵姹傛帹鑽悀"; CommentId=1} $U1 | Out-Null
Write-Host "  璇?: user1 > 鍥炲璇?"
Invoke-Api POST "/api/bottles/2/comments" @{Content="澶ц儐鍘绘姌鑵惧惂锛佹垜28宀佽緸鎺夊浗浼佸伐浣滃幓鍋氱嫭绔嬪紑鍙戣€咃紝鍓嶄袱骞村緢鑻︼紝浣嗙幇鍦ㄥ洖澶寸湅鏄垜鍋氳繃鏈€姝ｇ‘鐨勫喅瀹氥€?} $ADM | Out-Null
Write-Host "  璇?: admin > 鐡跺瓙2"
Invoke-Api POST "/api/bottles/2/comments" @{Content="鏂逛究閫忛湶鍋氬摢鏂归潰鐨勫紑鍙戝悧锛熸垜涔熸湁绫讳技鐨勬兂娉曪紝鎯冲浜嗚В涓€浜涖€?; CommentId=3} $U1 | Out-Null
Write-Host "  璇?: user1 > 鍥炲璇?"
Invoke-Api POST "/api/bottles/2/comments" @{Content="涓昏鏄仛 SaaS 宸ュ叿锛岄潰鍚戞捣澶栧競鍦虹殑銆傚墠涓ゅ勾纭疄寰堥毦锛屼絾涓€鏃︽湁浜嗙涓€涓ǔ瀹氬鎴峰氨浼氬ソ寰堝銆?; CommentId=3; ParentReplyId=4} $ADM | Out-Null
Write-Host "  璇?: admin > 妤间腑妤煎洖澶嶈瘎4"
Invoke-Api POST "/api/bottles/3/comments" @{Content="鐚尗鐪熺殑鏄笘鐣屼笂鏈€娌绘剤鐨勭敓鐗╋紒鎴戜篃鎯冲吇涓€鍙紝浣嗘€曞伐浣滃お蹇欑収椤句笉濂藉畠銆?} $ADM | Out-Null
Write-Host "  璇?: admin > 鐡跺瓙3"
Invoke-Api POST "/api/bottles/4/comments" @{Content="鍚屾劅锛佸幓骞村洖姣嶆牎鍙戠幇甯稿幓鐨勯偅瀹剁厧楗兼灉瀛愭憡杩樺湪锛屽ぇ鐖烽棶鎴戣繕鍔犺泲鍚楋紝宸偣娌＄环浣忋€?} $U1 | Out-Null
Write-Host "  璇?: user1 > 鐡跺瓙4"
Invoke-Api POST "/api/bottles/5/comments" @{Content="绾㈢儳鑲夎鐐掔硸鑹茬殑锛佹垜澶辫触杩囦笁娆℃墠鎴愬姛锛屽姞娌箏涓嬫涓€瀹氫細鏇村ソ锛?} $ADM | Out-Null
Write-Host "  璇?: admin > 鐡跺瓙5"

# 鈹€鈹€ 5. Likes 鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€
Write-Host "[5/5] 鐐硅禐..." -ForegroundColor Cyan
1..4 | ForEach-Object { Invoke-Api POST "/api/bottles/$_/like" $null $ADM | Out-Null }
@(1,2,3,5) | ForEach-Object { Invoke-Api POST "/api/bottles/$_/like" $null $U1 | Out-Null }
Write-Host "  鐐硅禐瀹屾垚"

Write-Host ""
Write-Host "========================================"
Write-Host "  瀹屾垚锛佽闂?http://localhost:5185"
Write-Host "  绠＄悊鍛樿处鍙? admin / 123456"
Write-Host "  鏅€氱敤鎴?   user1 / 123456"
Write-Host "========================================"
