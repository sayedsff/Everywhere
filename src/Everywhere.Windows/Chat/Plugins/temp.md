#### GUI 交互功能
1. **send_notification** - 发送 Windows Toast 通知（待实现）
2. **show_message_box** - 显示系统消息框（支持多种按钮和图标）

#### 窗口管理功能
3. **get_window_list** - 获取所有可见窗口列表
4. **manage_window** - 控制窗口状态和位置（显示/隐藏/最小化/最大化/移动）

#### 剪贴板功能
5. **get_clipboard_text** - 读取剪贴板文本
6. **set_clipboard_text** - 写入剪贴板文本

#### 系统设置功能
7. **set_system_volume** - 设置系统音量 (0-100)（存在问题）
8. **set_display_brightness** - 设置屏幕亮度 (0-100)（存在问题）
9. **set_desktop_wallpaper** - 更换桌面壁纸（未测试）

#### 鼠标和键盘操作
10. **move_cursor** - 移动鼠标光标到指定坐标
11. **click_mouse** - 执行鼠标点击（左/右/中键，单击/双击）
12. **send_keys** - 模拟键盘输入（支持特殊键如 Ctrl, Alt, Enter 等）

#### 电源管理功能 
13. **manage_power** - 管理电源状态（睡眠/休眠/关机/重启）（未测试）
14. **turn_off_monitor** - 关闭显示器（未测试）