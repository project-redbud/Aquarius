@echo off
setlocal enabledelayedexpansion
chcp 65001 >nul
set HOST=http://localhost:5185
set TMPDIR=%TEMP%\aquarius_test
mkdir "%TMPDIR%" 2>nul

echo ========================================
echo   Aquarius 测试数据生成
echo ========================================
echo.

echo [1/5] 注册用户...
echo {"username":"admin","email":"admin@test.com","password":"123456","confirmPassword":"123456"} > "%TMPDIR%\reg_admin.json"
echo {"username":"user1","email":"user1@test.com","password":"123456","confirmPassword":"123456"} > "%TMPDIR%\reg_user1.json"
curl -s -X POST "%HOST%/api/auth/register" -H "Content-Type: application/json" -d "@%TMPDIR%\reg_admin.json" >nul
echo   admin 注册完成
curl -s -X POST "%HOST%/api/auth/register" -H "Content-Type: application/json" -d "@%TMPDIR%\reg_user1.json" >nul
echo   user1 注册完成
echo.

echo [2/5] 登录获取 token...
for /f "delims=" %%i in ('curl -s -X POST "%HOST%/api/auth/login" -H "Content-Type: application/json" -d "{\"login\":\"admin\",\"password\":\"123456\"}"') do set ADM_RAW=%%i
for /f "tokens=*" %%j in ('powershell -Command "('%ADM_RAW%' | ConvertFrom-Json).token"') do set ADM_TOKEN=%%j
echo   ADMIN_TOKEN=!ADM_TOKEN!

for /f "delims=" %%i in ('curl -s -X POST "%HOST%/api/auth/login" -H "Content-Type: application/json" -d "{\"login\":\"user1\",\"password\":\"123456\"}"') do set U1_RAW=%%i
for /f "tokens=*" %%j in ('powershell -Command "('%U1_RAW%' | ConvertFrom-Json).token"') do set U1_TOKEN=%%j
echo   USER1_TOKEN=!U1_TOKEN!
echo.

echo [3/5] 投瓶...
(
echo {"Content":"今天在地铁上看到一个人捧着一本纸质书在读，在这个人人低头刷手机的时代，突然觉得好珍贵。我也该把床头那本落灰的书翻开了。","AuthorName":"地铁旅人"}
) > "%TMPDIR%\b1.json"
curl -s -X POST "%HOST%/api/bottles" -H "Content-Type: application/json" -H "Authorization: Bearer !ADM_TOKEN!" -d "@%TMPDIR%\b1.json" >nul
echo   瓶子1 (admin)

(
echo {"Content":"最近总在深夜问自己：我到底想要什么样的生活？是安稳的朝九晚五，还是冒险的自由职业？朋友们都说我太爱折腾了，可不折腾怎么知道哪条路对呢。","AuthorName":"深夜思考者"}
) > "%TMPDIR%\b2.json"
curl -s -X POST "%HOST%/api/bottles" -H "Content-Type: application/json" -H "Authorization: Bearer !ADM_TOKEN!" -d "@%TMPDIR%\b2.json" >nul
echo   瓶子2 (admin)

(
echo {"Content":"养了两年的猫今天跳到我腿上睡着了，发出咕噜咕噜的声音。突然觉得，被一只小生命信任的感觉，比什么都治愈。希望你今天也有让你心软的小瞬间。"}
) > "%TMPDIR%\b3.json"
curl -s -X POST "%HOST%/api/bottles" -H "Content-Type: application/json" -H "Authorization: Bearer !ADM_TOKEN!" -d "@%TMPDIR%\b3.json" >nul
echo   瓶子3 (admin,匿名)

(
echo {"Content":"第一次尝试做红烧肉，结果把厨房搞得像灾难现场。虽然卖相不好，但味道竟然还行！生活大概就是这样吧——看着乱七八糟，尝起来却有滋有味。","AuthorName":"厨房冒险家"}
) > "%TMPDIR%\b4.json"
curl -s -X POST "%HOST%/api/bottles" -H "Content-Type: application/json" -H "Authorization: Bearer !U1_TOKEN!" -d "@%TMPDIR%\b4.json" >nul
echo   瓶子4 (user1)

(
echo {"Content":"毕业三年了，今天路过母校，发现校门口那家奶茶店还开着。阿姨居然还记得我，问我还是老样子？一瞬间眼眶就热了。有些地方，永远是你的避风港。","AuthorName":"毕业游子"}
) > "%TMPDIR%\b5.json"
curl -s -X POST "%HOST%/api/bottles" -H "Content-Type: application/json" -H "Authorization: Bearer !U1_TOKEN!" -d "@%TMPDIR%\b5.json" >nul
echo   瓶子5 (user1)
echo.

echo [4/5] 评论与回复...
echo {"Content":"深有同感！我最近也开始重新看纸质书了，那种翻页的感觉和墨香是电子屏幕永远替代不了的。"} > "%TMPDIR%\c1.json"
curl -s -X POST "%HOST%/api/bottles/1/comments" -H "Content-Type: application/json" -H "Authorization: Bearer !ADM_TOKEN!" -d "@%TMPDIR%\c1.json" >nul
echo   评1: admin ^> 瓶子1

echo {"Content":"能问问你看的什么书吗？求推荐~","CommentId":1} > "%TMPDIR%\c2.json"
curl -s -X POST "%HOST%/api/bottles/1/comments" -H "Content-Type: application/json" -H "Authorization: Bearer !U1_TOKEN!" -d "@%TMPDIR%\c2.json" >nul
echo   评2: user1 ^> 回复评1

echo {"Content":"大胆去折腾吧！我28岁辞掉国企工作去做独立开发者，前两年很苦，但现在回头看是我做过最正确的决定。"} > "%TMPDIR%\c3.json"
curl -s -X POST "%HOST%/api/bottles/2/comments" -H "Content-Type: application/json" -H "Authorization: Bearer !ADM_TOKEN!" -d "@%TMPDIR%\c3.json" >nul
echo   评3: admin ^> 瓶子2

echo {"Content":"方便透露做哪方面的开发吗？我也有类似的想法，想多了解一些。","CommentId":3} > "%TMPDIR%\c4.json"
curl -s -X POST "%HOST%/api/bottles/2/comments" -H "Content-Type: application/json" -H "Authorization: Bearer !U1_TOKEN!" -d "@%TMPDIR%\c4.json" >nul
echo   评4: user1 ^> 回复评3

echo {"Content":"主要是做 SaaS 工具，面向海外市场的。前两年确实很难，但一旦有了第一个稳定客户就会好很多。","CommentId":3,"ParentReplyId":4} > "%TMPDIR%\c5.json"
curl -s -X POST "%HOST%/api/bottles/2/comments" -H "Content-Type: application/json" -H "Authorization: Bearer !ADM_TOKEN!" -d "@%TMPDIR%\c5.json" >nul
echo   评5: admin ^> 楼中楼回复评4

echo {"Content":"猫猫真的是世界上最治愈的生物！我也想养一只，但怕工作太忙照顾不好它。"} > "%TMPDIR%\c6.json"
curl -s -X POST "%HOST%/api/bottles/3/comments" -H "Content-Type: application/json" -H "Authorization: Bearer !ADM_TOKEN!" -d "@%TMPDIR%\c6.json" >nul
echo   评6: admin ^> 瓶子3

echo {"Content":"同感！去年回母校发现常去的那家煎饼果子摊还在，大爷问我还加蛋吗，差点没绷住。"} > "%TMPDIR%\c7.json"
curl -s -X POST "%HOST%/api/bottles/4/comments" -H "Content-Type: application/json" -H "Authorization: Bearer !U1_TOKEN!" -d "@%TMPDIR%\c7.json" >nul
echo   评7: user1 ^> 瓶子4

echo {"Content":"红烧肉要炒糖色的！我失败过三次才成功，加油~下次一定会更好！"} > "%TMPDIR%\c8.json"
curl -s -X POST "%HOST%/api/bottles/5/comments" -H "Content-Type: application/json" -H "Authorization: Bearer !ADM_TOKEN!" -d "@%TMPDIR%\c8.json" >nul
echo   评8: admin ^> 瓶子5
echo.

echo [5/5] 点赞...
for %%b in (1 2 3 4) do curl -s -X POST "%HOST%/api/bottles/%%b/like" -H "Authorization: Bearer !ADM_TOKEN!" >nul
for %%b in (1 2 3 5) do curl -s -X POST "%HOST%/api/bottles/%%b/like" -H "Authorization: Bearer !U1_TOKEN!" >nul
echo   点赞完成
echo.

rmdir /s /q "%TMPDIR%" 2>nul
echo ========================================
echo   完成！访问 http://localhost:5185
echo   管理员账号: admin / 123456
echo   普通用户:   user1 / 123456
echo ========================================
