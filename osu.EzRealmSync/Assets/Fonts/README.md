# 字体说明

默认使用 **系统已安装字体**（如微软雅黑）在 Roboto 缺字时即时栅格化，一般**不必**再复制 Noto。

## 可选：Noto 位图字体

若需与 osu! 完全一致的位图字体，设置环境变量 `EZREALMSYNC_USE_NOTO_FONTS=1`，并按下列方式之一提供 Noto：

### 从 osu-resources 复制

```powershell
# 在 EzRealmSync 根目录执行
$src = "..\..\osu-resources\osu.Game.Resources\Fonts\Noto"
$dst = "osu.EzRealmSync\Assets\Fonts\Noto"
New-Item -ItemType Directory -Force -Path $dst | Out-Null
Copy-Item "$src\Noto-CJK-Basic*" $dst
Copy-Item "$src\Noto-CJK-Compatibility*" $dst
Copy-Item "$src\Noto-Basic*" $dst
```

### 环境变量

`EZREALMSYNC_FONTS` 指向包含 `Noto` 子目录的 `Fonts` 根路径。

### 资源程序集 DLL

将 `osu.Game.Resources.dll` 放到 exe 同目录（仅字体，不必引用 `osu.Game`）。
