# Aquarius 测试数据脚本

> 在项目根目录下执行。需先启动后端 `cd backend && dotnet run`。

```bash
#!/bin/bash
HOST="http://localhost:5185"
D=$(mktemp -d)

echo "=== 注册 ==="
curl -s -X POST "$HOST/api/auth/register" -H "Content-Type: application/json" \
  -d '{"username":"admin","email":"admin@test.com","password":"123456","confirmPassword":"123456"}'
curl -s -X POST "$HOST/api/auth/register" -H "Content-Type: application/json" \
  -d '{"username":"user1","email":"user1@test.com","password":"123456","confirmPassword":"123456"}'

echo "=== 登录 ==="
ADM=$(curl -s -X POST "$HOST/api/auth/login" -H "Content-Type: application/json" \
  -d '{"login":"admin","password":"123456"}' | grep -o '"token":"[^"]*"' | cut -d'"' -f4)
U1=$(curl -s -X POST "$HOST/api/auth/login" -H "Content-Type: application/json" \
  -d '{"login":"user1","password":"123456"}' | grep -o '"token":"[^"]*"' | cut -d'"' -f4)

post() { curl -s -X POST "$1" -H "Content-Type: application/json" ${2:+-H "Authorization: Bearer $2"} -d "@$3"; }

# ---- 投瓶 ----
cat > "$D/b1.json" << 'EOF'
{"Content":"今天在地铁上看到一个人捧着一本纸质书在读，在这个人人低头刷手机的时代，突然觉得好珍贵。我也该把床头那本落灰的书翻开了。","AuthorName":"地铁旅人"}
EOF
post "$HOST/api/bottles" "$ADM" "$D/b1.json" > /dev/null

cat > "$D/b2.json" << 'EOF'
{"Content":"最近总在深夜问自己：我到底想要什么样的生活？是安稳的朝九晚五，还是冒险的自由职业？朋友们都说我太爱折腾了，可不折腾怎么知道哪条路对呢。","AuthorName":"深夜思考者"}
EOF
post "$HOST/api/bottles" "$ADM" "$D/b2.json" > /dev/null

cat > "$D/b3.json" << 'EOF'
{"Content":"养了两年的猫今天跳到我腿上睡着了，发出咕噜咕噜的声音。突然觉得，被一只小生命信任的感觉，比什么都治愈。希望你今天也有让你心软的小瞬间。"}
EOF
post "$HOST/api/bottles" "$ADM" "$D/b3.json" > /dev/null

cat > "$D/b4.json" << 'EOF'
{"Content":"第一次尝试做红烧肉，结果把厨房搞得像灾难现场。虽然卖相不好，但味道竟然还行！生活大概就是这样吧——看着乱七八糟，尝起来却有滋有味。","AuthorName":"厨房冒险家"}
EOF
post "$HOST/api/bottles" "$U1" "$D/b4.json" > /dev/null

cat > "$D/b5.json" << 'EOF'
{"Content":"毕业三年了，今天路过母校，发现校门口那家奶茶店还开着。阿姨居然还记得我，问我还是老样子？一瞬间眼眶就热了。有些地方，永远是你的避风港。","AuthorName":"毕业游子"}
EOF
post "$HOST/api/bottles" "$U1" "$D/b5.json" > /dev/null

# ---- 评论 ----
cat > "$D/c1.json" << 'EOF'
{"Content":"深有同感！我最近也开始重新看纸质书了，那种翻页的感觉和墨香是电子屏幕永远替代不了的。"}
EOF
post "$HOST/api/bottles/1/comments" "$ADM" "$D/c1.json" > /dev/null

cat > "$D/c2.json" << 'EOF'
{"Content":"能问问你看的什么书吗？求推荐~","CommentId":1}
EOF
post "$HOST/api/bottles/1/comments" "$U1" "$D/c2.json" > /dev/null

cat > "$D/c3.json" << 'EOF'
{"Content":"大胆去折腾吧！我28岁辞掉国企工作去做独立开发者，前两年很苦，但现在回头看是我做过最正确的决定。"}
EOF
post "$HOST/api/bottles/2/comments" "$ADM" "$D/c3.json" > /dev/null

cat > "$D/c4.json" << 'EOF'
{"Content":"方便透露做哪方面的开发吗？我也有类似的想法，想多了解一些。","CommentId":3}
EOF
post "$HOST/api/bottles/2/comments" "$U1" "$D/c4.json" > /dev/null

cat > "$D/c5.json" << 'EOF'
{"Content":"主要是做 SaaS 工具，面向海外市场的。前两年确实很难，但一旦有了第一个稳定客户就会好很多。","CommentId":3,"ParentReplyId":4}
EOF
post "$HOST/api/bottles/2/comments" "$ADM" "$D/c5.json" > /dev/null

cat > "$D/c6.json" << 'EOF'
{"Content":"猫猫真的是世界上最治愈的生物！我也想养一只，但怕工作太忙照顾不好它。"}
EOF
post "$HOST/api/bottles/3/comments" "$ADM" "$D/c6.json" > /dev/null

cat > "$D/c7.json" << 'EOF'
{"Content":"同感！去年回母校发现常去的那家煎饼果子摊还在，大爷问我还加蛋吗，差点没绷住。"}
EOF
post "$HOST/api/bottles/4/comments" "$U1" "$D/c7.json" > /dev/null

cat > "$D/c8.json" << 'EOF'
{"Content":"红烧肉要炒糖色的！我失败过三次才成功，加油~下次一定会更好！"}
EOF
post "$HOST/api/bottles/5/comments" "$ADM" "$D/c8.json" > /dev/null

# ---- 点赞 ----
for b in 1 2 3 4; do curl -s -X POST "$HOST/api/bottles/$b/like" -H "Authorization: Bearer $ADM" > /dev/null; done
for b in 1 2 3 5; do curl -s -X POST "$HOST/api/bottles/$b/like" -H "Authorization: Bearer $U1" > /dev/null; done

rm -rf "$D"
echo "Done. admin / 123456 | user1 / 123456"
```

---

## 账号

| 角色 | 用户名 | 密码 | 备注 |
|------|--------|------|------|
| 管理员 | admin | 123456 | 可访问 `/admin` 管理面板 |
| 普通用户 | user1 | 123456 | 可在 `/my` 管理自己的瓶子/评论 |

## 数据概览

| 瓶子 | 作者 | 评论结构 |
|------|------|----------|
| 1 地铁书 | admin | 评1(admin) → 评2(user1 回复) |
| 2 自由职业 | admin | 评3(admin) → 评4(user1 回复) → 评5(admin 楼中楼) |
| 3 猫 | admin(匿名) | 评6 |
| 4 红烧肉 | user1 | 评7 |
| 5 母校 | user1 | 评8 |
