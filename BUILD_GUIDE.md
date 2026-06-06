# CE Web Browser - 编译与发布指南

📦 完整的编译、打包和发布说明

---

## 关于依赖

✅ **本mod不需要其他mod支持！**

`WebOverlay.json` 中 `"dependencies": []` 是空的，只需游戏自带的三个 DLL 文件即可正常工作。

---

## 前置要求

### 1. 必需软件
- **.NET 8.0 SDK** - 下载地址：https://dotnet.microsoft.com/download/dotnet/8.0
- **Slay the Spire 2 游戏** - 版本 0.105.0 或更高

### 2. 验证安装

在终端/命令提示符中运行：
```bash
dotnet --version
```

应该输出类似 `8.0.xxx` 的版本号。

---

## 完整编译步骤

### 方法一：使用游戏目录（推荐）

#### 步骤 1：找到游戏目录

**Windows:**
```
C:\Program Files (x86)\Steam\steamapps\common\SlayTheSpire2\
```

**Linux:**
```
~/.steam/steam/steamapps/common/SlayTheSpire2/
```

**macOS:**
```
~/Library/Application Support/Steam/steamapps/common/SlayTheSpire2/
```

#### 步骤 2：确认游戏 DLL 文件存在

在游戏目录中找到以下文件（根据你的平台）：
- `data_sts2_windows_x86_64/` (Windows)
- `data_sts2_linux_x86_64/` (Linux)
- `data_sts2_osx_universal/` (macOS)

该目录应包含：
- `GodotSharp.dll`
- `sts2.dll`
- `0Harmony.dll`

#### 步骤 3：编辑项目文件（可选）

如果你把 mod 放在游戏目录外，编辑 `WebOverlay.csproj`，修改这一行：
```xml
<Sts2Dir Condition="'$(Sts2Dir)' == ''">你的游戏完整路径</Sts2Dir>
```

例如：
```xml
<Sts2Dir Condition="'$(Sts2Dir)' == ''">C:\Program Files (x86)\Steam\steamapps\common\SlayTheSpire2</Sts2Dir>
```

#### 步骤 4：编译

在 mod 目录下打开终端，运行：

```bash
dotnet build WebOverlay.csproj --configuration Release
```

#### 步骤 5：找到编译好的 DLL

编译成功后，DLL 文件位于：
```
bin/Release/net8.0/WebOverlay.dll
```

---

### 方法二：命令行指定游戏路径

你也可以在编译时直接指定游戏路径：

```bash
dotnet build WebOverlay.csproj --configuration Release -p:Sts2Dir="你的游戏完整路径"
```

**Windows 示例：**
```bash
dotnet build WebOverlay.csproj --configuration Release -p:Sts2Dir="C:\Program Files (x86)\Steam\steamapps\common\SlayTheSpire2"
```

**Linux 示例：**
```bash
dotnet build WebOverlay.csproj --configuration Release -p:Sts2Dir="$HOME/.steam/steam/steamapps/common/SlayTheSpire2"
```

---

## 创建发布包

### 完整发布包结构

```
WebOverlay/
├── WebOverlay.dll          # 编译后的插件
├── WebOverlay.json         # 插件配置
├── README.md               # 说明文档
└── TUTORIAL.md             # 使用教程
```

### 打包步骤

#### 1. 创建发布目录
```bash
mkdir WebOverlay-Release
```

#### 2. 复制必要文件
```bash
# 复制编译好的 DLL
cp bin/Release/net8.0/WebOverlay.dll WebOverlay-Release/

# 复制配置和文档
cp WebOverlay.json WebOverlay-Release/
cp README.md WebOverlay-Release/
cp TUTORIAL.md WebOverlay-Release/
```

#### 3. 打包为 ZIP（可选，用于发布）

**Windows (PowerShell):**
```powershell
Compress-Archive -Path WebOverlay-Release\* -DestinationPath WebOverlay-v5.0.0.zip
```

**Linux/macOS:**
```bash
cd WebOverlay-Release
zip -r ../WebOverlay-v5.0.0.zip .
```

---

## 安装到游戏

### 方法一：直接复制（推荐）

1. 编译完成后，将以下文件复制到游戏的 `mods` 文件夹：
   - `WebOverlay.dll`
   - `WebOverlay.json`

2. 最终结构应该是：
```
SlayTheSpire2/
└── mods/
    └── WebOverlay/
        ├── WebOverlay.dll
        └── WebOverlay.json
```

### 方法二：使用完整发布包

1. 解压 `WebOverlay-v5.0.0.zip`
2. 将整个 `WebOverlay` 文件夹复制到游戏的 `mods` 目录

---

## 测试安装

1. 启动 Slay the Spire 2
2. 在主菜单进入「模组」或「Mods」菜单
3. 找到「CE Web Browser」并启用
4. 重启游戏
5. 测试打开浏览器面板

---

## 常见编译问题

### Q: 找不到游戏 DLL 文件？
**A:** 
- 确认游戏已安装且版本正确
- 检查 `Sts2Dir` 路径是否正确
- 确认路径中没有特殊字符或空格问题（Windows 建议用引号包裹）

### Q: 编译时提示缺少 GodotSharp.dll？
**A:**
- 检查 `WebOverlay.csproj` 中的引用路径
- 确认游戏目录结构正确
- 确保使用的是 Release 配置编译

### Q: 编译成功但游戏无法加载？
**A:**
- 检查 `WebOverlay.json` 中的版本号和依赖
- 确认 DLL 文件名正确
- 查看游戏日志了解详细错误

### Q: 如何清理编译缓存？
**A:**
```bash
# 删除编译输出
rm -rf bin/ obj/

# 或者重新编译
dotnet clean
dotnet build --configuration Release
```

---

## GitHub Releases 发布流程

### 发布准备
1. 确保所有代码已提交并推送到 main 分支
2. 更新 `WebOverlay.json` 中的版本号
3. 创建发布包 ZIP 文件

### 在 GitHub 上创建 Release
1. 访问 https://github.com/it71/trysljjt2web/releases
2. 点击「Draft a new release」
3. 填写版本标签：`v5.0.0`
4. 填写发布标题：`CE Web Browser v5.0.0`
5. 在描述中列出主要更新内容
6. 上传 `WebOverlay-v5.0.0.zip` 文件
7. 点击「Publish release」

---

## 版本号说明

格式：`主版本.次版本.修订号`

- **主版本**：重大功能更新或架构变更
- **次版本**：新功能添加
- **修订号**：Bug 修复或小改进

---

祝你编译顺利！🎮✨

如有问题，请提交 Issue 反馈。
