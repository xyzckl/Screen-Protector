# ScreenProtector

[English](#english) | [中文](#chinese)

---

<a id="english"></a>
## ScreenProtector (English)

ScreenProtector is a WinUI 3 (Windows App SDK) based desktop application that provides a customizable, topmost screen overlay with various retro visual effects. Built with C# and .NET 10, it uses `Microsoft.Graphics.Win2D` for high-performance rendering.

### Features
* **Retro Visual Effects**: Choose between classic CRT scanlines or a nostalgic Pixelate effect.
* **Highly Customizable**: Adjust scanline speed, width, opacity, and choose color filters (e.g., Classic Amber, Matrix Green). For the pixelate effect, tweak the pixel size and apply monochrome colors like Gameboy Green or Amber.
* **Click-Through Overlay**: The overlay can be set to allow mouse clicks to pass through, ensuring it doesn't interfere with your normal workflow.
* **Performance Tuning**: Configure capture and output frame rates independently.
* **Global Shortcuts**: Set a custom global shortcut to quickly toggle the overlay on and off.
* **System Integration**: Support for running in the background (system tray) and launching at startup.
* **Bilingual Support**: Built-in support for English and Chinese.

### Technical Stack
* **Framework**: WinUI 3 (Windows App SDK)
* **Runtime**: .NET 10.0
* **Graphics**: Microsoft.Graphics.Win2D
* **Language**: C#

### Building and Running
To build and run this project, you need:
1. Windows 10 (version 1809 or later) or Windows 11.
2. Visual Studio 2022 with the "Windows application development" workload.
3. .NET 10 SDK.

Clone the repository and open the solution in Visual Studio, then build and run the `ScreenProtector` project.

---

<a id="chinese"></a>
## ScreenProtector (中文)

ScreenProtector 是一款基于 WinUI 3 (Windows App SDK) 的桌面应用程序，它提供了一个可自定义的置顶屏幕遮罩，并在屏幕上应用各种复古视觉效果。项目使用 C# 和 .NET 10 编写，并利用 `Microsoft.Graphics.Win2D` 进行高性能渲染。

### 主要功能
* **复古视觉效果**：提供经典的 CRT 扫描线效果或怀旧的像素化效果。
* **高度可定制**：可调节扫描线的速度、宽度、不透明度，并选择颜色滤镜（如经典琥珀色、矩阵绿色）。对于像素化效果，可以调整像素大小并应用单色模式（如掌机绿色或琥珀色）。
* **鼠标穿透**：遮罩层支持开启鼠标点击穿透，确保不会干扰您的正常工作和操作。
* **性能调节**：可独立配置屏幕捕获帧率和输出渲染帧率。
* **全局快捷键**：支持设置自定义全局快捷键，以快速开启或关闭屏幕遮罩。
* **系统集成**：支持后台运行（系统托盘）以及开机自启动。
* **双语支持**：内置英文和中文支持。

### 技术栈
* **框架**：WinUI 3 (Windows App SDK)
* **运行时**：.NET 10.0
* **图形渲染**：Microsoft.Graphics.Win2D
* **开发语言**：C#

### 编译与运行
要编译和运行此项目，您需要：
1. Windows 10 (1809 或更高版本) 或 Windows 11。
2. 安装了“Windows 应用程序开发”工作负载的 Visual Studio 2022。
3. .NET 10 SDK。

克隆仓库并在 Visual Studio 中打开解决方案，然后编译并运行 `ScreenProtector` 项目。
