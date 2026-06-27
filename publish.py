import os, shutil, subprocess, sys

ROOT = os.path.dirname(os.path.abspath(__file__))
DEPLOY = os.path.join(ROOT, "deploy")

def run(cmd, cwd=None):
    print(f"  $ {cmd}")
    r = subprocess.run(cmd, shell=True, cwd=cwd or ROOT)
    if r.returncode != 0:
        print(f"  FAILED (exit {r.returncode})")
        sys.exit(1)

print("=" * 50)
print("  Aquarius 一键打包")
print("=" * 50)

# 0. Ensure production env is clean (no Android build leftovers)
env_prod = os.path.join(ROOT, "frontend", "src", "environments", "environment.prod.ts")
with open(env_prod, "w", encoding="utf-8") as f:
    f.write("export const environment = {\n  production: true,\n  apiBase: ''\n};\n")

# 1. Angular
print("\n[1/3] 构建 Angular 前端...")
run("npm run build -- --configuration production", cwd=os.path.join(ROOT, "frontend"))

# 2. .NET
print("\n[2/3] 发布 .NET 后端...")
run("dotnet publish backend/Aquarius.Api.csproj -c Release -o backend/release")

# 3. Pack
print(f"\n[3/3] 打包到 {DEPLOY} ...")
if os.path.exists(DEPLOY):
    shutil.rmtree(DEPLOY)
os.makedirs(DEPLOY)

for item in os.listdir(os.path.join(ROOT, "backend", "release")):
    src = os.path.join(ROOT, "backend", "release", item)
    dst = os.path.join(DEPLOY, item)
    if os.path.isdir(src):
        shutil.copytree(src, dst)
    else:
        shutil.copy2(src, dst)

wwwroot_src = os.path.join(ROOT, "frontend", "dist", "frontend", "browser")
wwwroot_dst = os.path.join(DEPLOY, "wwwroot")
if os.path.exists(wwwroot_dst):
    shutil.rmtree(wwwroot_dst)
shutil.copytree(wwwroot_src, wwwroot_dst)

print(f"""
{'=' * 50}
  打包完成!
  部署目录: {DEPLOY}

  上传到服务器后执行:
    dotnet Aquarius.Api.dll
{'=' * 50}""")
