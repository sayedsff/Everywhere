## [v0.4.2](https://github.com/DearVa/Everywhere/releases/tag/v0.4.2) - 2025-10-28

### ✨ Features
- Now you can upload documents (PDF, Word, Text, etc.) directly in the chat window as attachments for context (⚠️ only supported by models that allow file inputs)

### 🚀 Improvements
- Chat window can be closed when press shortcut again
- Optimized encoding handling in "File system" chat plugin

### 🐛 Bug Fixes
- Fixed chat topic may not generate correctly for some models
- Fixed "Web search" chat tool may report `count out of range` error
- Fixed "Scroll to end" button in chat window mistakenly get focused
- (Windows) Fixed missing `Everything.dll`
- (Windows) Fixed chat window still appears in `Alt+Tab` list when closed
- (Windows) Fixed chat window disappears when picking a file
- (Windows) Fixed icon & title of update notify is missing

**Full Changelog**: https://github.com/DearVa/Everywhere/compare/v0.4.1...v0.4.2



## [v0.4.1](https://github.com/DearVa/Everywhere/releases/tag/v0.4.1) - 2025-10-27

### ⚠️ BREAKING CHANGE: Chat window shortcut will reset to `Ctrl+Shift+E` due to renaming "Hotkey" to "Shortcut".
️
### 🚀 Improvements
- Renamed DeepSeek models to their new official names
- I18N: Changed "Hotkey" to "Shortcut"
- Refactored "operate UI elements" chat tool for better stability

### 🐛 Bug Fixes
- Fixed window cannot maximize when clicking the maximize button
- Fixed "Web snapshot" chat tool not working
- Fixed "everything" chat tool cannot work when "file system" chat tool is enabled
- Fixed token counting may be bigger than actual usage in some cases

**Full Changelog**: https://github.com/DearVa/Everywhere/compare/v0.4.0...v0.4.1



## [v0.4.0](https://github.com/DearVa/Everywhere/releases/tag/v0.4.0) - 2025-10-26

### ✨ Features
- Plugin Execution Feedback
  - High-permission plugins now require user confirmation before running
  - Results of plugin calls are now displayed in the chat window 
    - File system plugin lists which files were accessed
    - File changes are shown and can be reviewed before applying
    - Web search now displays the specific query being used
- Temporary Chat
  - Temporary chats that are not saved will now be automatically deleted when switching to another chat or creating a new one
  - You can choose to automatically enter temporary chat mode in settings
- Web Search Enhancements
  - Added Jina as a web search provider
  - Added SearXNG as a web search provider
- Added settings for controlling visual context usage and detail level
- Chat window now displays the current chat title
- (Windows) Integrated Everything to accelerate local file searches

### 🚀 Improvements
- Improved the main window UI layout & style
- Enabled right-click context menu (copy, cut, paste) in the chat input box
- Added a scroll-to-bottom button in the chat window
- Added more emoji choices for custom assistants

### 🐛 Bug Fixes
- Fixed chat history was sometimes not sorted correctly
- Fixed the `Alt` key could not be used as a hotkey
- Fixed the default assistant's icon could not be changed
- Fixed the element picker could not be closed with a right-click
- Fixed the main window would appear after picking an element
- Fixed potential unresponsiveness issues during chats
- Fixed connection test failures for Custom Assistants
- Fixed chat title generation may fail for some models
- Fixed fonts may become _Italic_ unexpectedly
- Fixed a dead link in the Welcome Dialog
- Fixed a recursive self-reference issue
- Fixed wrong acrylic effect on Windows 10

**Full Changelog**: https://github.com/DearVa/Everywhere/compare/v0.3.12...v0.4.0



## [v0.3.12](https://github.com/DearVa/Everywhere/releases/tag/v0.3.12) - 2025-10-16

### 🚀 Improvements
- Removed the obsolete Bing web search engine
- Optimized error handling

### 🐛 Bug Fixes
- Fixed an issue where the chat window could not be resized
- Fixed an issue where the Tavily search engine could not be invoked
- Fixed an issue where the chat action bubble did not display error messages
- Fixed an issue where variables in the system prompt were not rendered
- Fixed an issue where the chat topic summary was sometimes empty (Note: This is not fully resolved, as some models may still produce empty results)

**Full Changelog**: https://github.com/DearVa/Everywhere/compare/v0.3.11...v0.3.12



## [v0.3.11](https://github.com/DearVa/Everywhere/releases/tag/v0.3.11) - 2025-10-16

### ⚠️ Breaking Changes ⚠️
Due to the model configuration page being rebuilt, previously configured model settings (including API keys, etc.) will be lost! However, they still exist in the software settings file. Advanced users can find them at `C:\Users\<username>\AppData\Roaming\Everywhere\settings.json`.

### ✨ Features
- 🎉 Added custom assistants! You can now create multiple assistants with different icons, names, and prompts, and switch between them freely during a chat
- Added support for the Tavily web search engine

### 🚀 Improvements
- Optimized exception handling

**Full Changelog**: https://github.com/DearVa/Everywhere/compare/v0.3.10...v0.3.11



## [v0.3.10](https://github.com/DearVa/Everywhere/releases/tag/v0.3.10) - 2025-10-14

### 🚀 Improvements
- Introduced a new, modern installer that remembers the previous installation location during updates

### 🐛 Bug Fixes
- Fixed an issue where an error was thrown if the OpenAI API key was empty (which is allowed for services like LM Studio)
- Fixed a bug that prevented pasting images as attachments in some cases
- Fixed a bug that caused the application to freeze when sending messages with images
- Fixed an issue causing an HTTP 400 error during function calls
- Fixed an issue where requests could be blocked by Cloudflare from some third-party model providers

**Full Changelog**: https://github.com/DearVa/Everywhere/compare/v0.3.9...v0.3.10



## [v0.3.9](https://github.com/DearVa/Everywhere/releases/tag/v0.3.9) - 2025-10-13

### ✨ Features
- Provider icons in settings are now loaded as local resources for faster display
- Added deep-thought output support for Ollama, SiliconFlow, and some OpenAI-compatible models; fixed SiliconFlow and similar models not outputting results
- Added option to show chat plugin permissions in settings

### 🚀 Improvements
- Enhanced error handling and user-friendly messages

### 🐛 Bug Fixes
- Fixed dialog covering the title bar, making the window undraggable or unresponsive
- Fixed some prompt tasks (e.g. translation) may use the wrong target language

**Full Changelog**: https://github.com/DearVa/Everywhere/compare/v0.3.8...v0.3.9



## [v0.3.8](https://github.com/DearVa/Everywhere/releases/tag/v0.3.8) - 2025-10-11

### ✨ Features
- Software updates can now be cancelled by dismissing the toast notification
- Added more keyboard shortcuts: `Ctrl+N` for a new chat, `Ctrl+T` to for tools switch
- Added a visual tree length limit setting to save tokens
- Added a notification when an update is available

### 🚀 Improvements
- Optimized the button layout in the chat window
- Added more friendly error messages for a better user experience

### 🐛 Bug Fixes
- Fixed a potential error when loading settings
- Fixed an issue where the chat window could not be reopened after being accidentally closed
- Fixed a missing scrollbar on the chat plugin page (#28)
- Fixed unnecessary telemetry logging
- Corrected a typo for an Ollama model: deepseek R1 8B -> deepseek R1 7B

**Full Changelog**: https://github.com/DearVa/Everywhere/compare/v0.3.7...v0.3.8



## [v0.3.7](https://github.com/DearVa/Everywhere/releases/tag/v0.3.7) - 2025-10-11

### 🐞 Fixed
- Fixed error messages were incorrectly parsed as "unknown".

**Full Changelog**: https://github.com/DearVa/Everywhere/compare/v0.3.6...v0.3.7



## [v0.3.6](https://github.com/DearVa/Everywhere/releases/tag/v0.3.6) - 2025-10-10

### ✨ New Features
- Added chat statistics in the chat window, which can be toggled in settings.
- Added a setting to control whether to automatically attach the focused element when opening the chat window.
- Added a setting to allow the model to continue generating responses in the background after the chat window is closed.
- Added support for `Claude Sonnet 4.5`.

### 🔄️ Changed
- Improved tooltips for plugin settings.
- Most error messages are now translated and provide more detailed hints.
- Improved the download speed and stability of in-app updates.
- Model parameter settings are now expanded by default to prevent them from being missed.

### 🐞 Fixed
- Fixed an issue where the model's tool-call usage was displayed in the wrong position.
- Fixed an issue where the chat window could not be reopened after being closed while a message was being streamed.
- Fixed an issue where the `Shift` and `Win` keys could become unresponsive if a hotkey included the `Win` key. You can now set the Copilot key as a hotkey normally.

**Full Changelog**: https://github.com/DearVa/Everywhere/compare/v0.3.5...v0.3.6



## [v0.3.5](https://github.com/DearVa/Everywhere/releases/tag/v0.3.5) - 2025-10-09

### 🐞 Fixed
- Fixed hotkey input box crashes when clicking twice [#20](https://github.com/DearVa/Everywhere/issues/20)
- Fixed potential null pointer error when sending message
- Fixed wrong telemetry log level

**Full Changelog**: https://github.com/DearVa/Everywhere/compare/v0.3.4...v0.3.5



## [v0.3.4](https://github.com/DearVa/Everywhere/releases/tag/v0.3.4) - 2025-10-09

### 🔄️ Changed
- Improved user prompt for tool usage
- Improved settings saving & loading logic
- Added logging for telemetry
- Removed unnecessary telemetry data

### 🐞 Fixed
- Fixed chat title generation for non-OpenAI models will fail
- Fixed web search plugin may not work in some cases
- Fixed custom model not saved or applied
- Fixed visual tree plugin is not disabled correctly

**Full Changelog**: https://github.com/DearVa/Everywhere/compare/v0.3.3...v0.3.4



## [v0.3.3](https://github.com/DearVa/Everywhere/releases/tag/v0.3.3) - 2025-10-08

### ✨ New Features
- Added telemetry to help us improve. See [Data and Privacy](https://github.com/DearVa/Everywhere/blob/main/DATA_AND_PRIVACY.md)
- Unsent messages will be saved automatically

### 🔄️ Changed
- Improved sidebar UI and animation

### 🐞 Fixed
- Fixed update message in settings page may disappear when fetching new version

**Full Changelog**: https://github.com/DearVa/Everywhere/compare/v0.3.2...v0.3.3



## [v0.3.2](https://github.com/DearVa/Everywhere/releases/tag/v0.3.2) - 2025-10-05

### 🐞 Fixed
- Fixed chat input box watermark behavior error
- (Windows) Fixed powershell plugin missing modules

**Full Changelog**: https://github.com/DearVa/Everywhere/compare/v0.3.1...v0.3.2



## [v0.3.1](https://github.com/DearVa/Everywhere/releases/tag/v0.3.1) - 2025-10-04

### 🔄️ Changed
- Improved markdown rendering styles
- Improved OOBE experience
- Changed official website link to https://everywhere.sylinko.com/

**Full Changelog**: https://github.com/DearVa/Everywhere/compare/v0.3.0...v0.3.1



## [v0.3.0](https://github.com/DearVa/Everywhere/releases/tag/v0.3.0) - 2025-09-24

### ✨ New Features
- 🎉 New Icon
- Added acrylic effect to tray icon menu
- Added OOBE (Out-Of-Box Experience) for first time users, including:
  - The welcome Dialog
  - Quick Setup Wizard
- Added support for custom model
- Added chat attachments storage
- Added support for more hotkeys, such as `Copilot` key on Windows
- Added watchdog process
- Chat window can be resized manually
- Chat window will show in taskbar when pinned

### 🔄️ Changed
- Refactored Plugin System, including:
  - Added Plugin Manager in Settings
  - Added file system plugin for reading and writing files
  - Added code execution plugin with PowerShell on Windows
  - Added web browsing plugin with Puppeteer
  - Added visual element plugin for capturing screen content when UI automation is not available
  - Refactored web search plugin
- Refactored logging system with structured logging
- Improved visual capturing performance
- Improved acrylic effect visibility

### 🐞 Fixed
- Fixed removing or switching chat history frequently may cause crash
- Fixed emoji rendering issues in the chat window
- Fixed application may freeze when active chat window in some cases
- Fixed settings load/save issues
- Fixed new chat button disable state is not updated when switching chat history
- Fixed detecting focused element mistakenly in some cases
- Fixed chat window may auto scroll when selecting text

### ⚠️ Known Issues
- Chat messages may disappear when selecting text
- Chat window may flicker when pinned

**Full Changelog**: https://github.com/DearVa/Everywhere/compare/v0.2.4...v0.3.0



## [v0.2.4](https://github.com/DearVa/Everywhere/releases/tag/v0.2.4) - 2025-08-15

### ✨ New Features
- Added Change Log in Welcome Dialog

### 🔄️ Changed
- Apply warning level filter to EF Core logging

### 🐞 Fixed
- Fixed Google Gemini invoking issues
- Fixed Restart as Administrator may not work on some cases
- Fixed Dialog and Toast may crash the app when reopen after closed a window
- Fixed `ChatElementAttachment`'s overlay window may cover the `ChatWindow`
- Fixed `ChatElementAttachment`'s overlay window may not disappear

**Full Changelog**: https://github.com/DearVa/Everywhere/compare/v0.2.3...v0.2.4



## [v0.2.3](https://github.com/DearVa/Everywhere/releases/tag/v0.2.3) - 2025-08-14

### ✨ New Features
- Added settings for automatically startup
- Added settings for Software Update

### 🐞 Fixed
- Fixed markdown rendering issues in the Chat Window

**Full Changelog**: https://github.com/DearVa/Everywhere/compare/v0.2.2...v0.2.3



## [v0.2.2](https://github.com/DearVa/Everywhere/releases/tag/v0.2.2) - 2025-08-11

### ✨ New Features
- **Model Support**: Added support for `Claude Opus 4.1`

### 🔄️ Changed
- Split settings into separate sidebar items

**Full Changelog**: https://github.com/DearVa/Everywhere/compare/v0.2.1...v0.2.2



## [v0.2.1](https://github.com/DearVa/Everywhere/releases/tag/v0.2.1) - 2025-08-11

### ✨ New Features
- **Model Support**: Added support for `GPT-5` series models:
  - `GPT-5`
  - `GPT-5 mini`
  - `GPT-5 nano`

### 🐞 Fixed
- Fixed markdown rendering issues in the Chat Window

**Full Changelog**: https://github.com/DearVa/Everywhere/compare/v0.2.0...v0.2.1



## [v0.2.0](https://github.com/DearVa/Everywhere/releases/tag/v0.2.0) - 2025-08-10

This update introduces support for over 20 new models and a completely refactored settings page for a better user experience.

### ✨ New Features

We've integrated the following new models:

- **OpenAI**: `o4-mini`, `o3`, `GPT-4.1`, `GPT-4.1 mini`, `GPT-4o` (`GPT-5` series will be released in next version)
- **Anthropic**: `Claude Opus 4`, `Claude Sonnet 4`, `Claude 3.7 Sonnet`, `Claude 3.5 Haiku`
- **Google**: `Gemini 2.5 Pro`, `Gemini 2.5 Flash`, `Gemini 2.5 Flash-Lite`
- **DeepSeek**: `DeepSeek V3`, `DeepSeek R1`
- **Moonshot**: `Kimi K2`, `Kimi Latest`, `Kimi Thinking Preview`
- **xAI**: `Grok 4`, `Grok 3 Mini`, `Grok 3`
- **Ollama**: `GPT-OSS 20B`, `DeekSeek R1 7B`, `Qwen 3 8B`

### ⚠️ BREAKING CHANGE: Database Refactor

To improve performance and stability, the chat database has been refactored.

- **As this is a beta release, chat history from previous versions is no longer available.**
- The new database structure now supports data migrations, which will prevent data loss in future updates. We appreciate your understanding.

**Full Changelog**: https://github.com/DearVa/Everywhere/compare/v0.1.3...v0.2.0



## [v0.1.3](https://github.com/DearVa/Everywhere/releases/tag/v0.1.3) - 2025-08-08

### ✨ New Features
- Added a pin button to the Chat Window, to keep it always on top and not close on lost focus
- Added detailed error messages in the Chat Window
- Added auto enum settings support by @SlimeNull in #10

### 🐞 Fixed
- Fixed ChatInputBox max height

**Full Changelog**: https://github.com/DearVa/Everywhere/compare/v0.1.2...v0.1.3



## [v0.1.2](https://github.com/DearVa/Everywhere/releases/tag/v0.1.2) - 2025-08-02

### ✨ New Features
- Added a notification when the app is first hide to the system tray

### 🔄️ Changed
- (Style) Decreased the background opacity of the main window, for Mica effect

### 🐞 Fixed
- Fixed wrong links in Welcome Dialog

### ⚠️ Known Issues
- The opacity of tray icon menu is broken

**Full Changelog**: https://github.com/DearVa/Everywhere/compare/v0.1.1...v0.1.2



## [v0.1.1](https://github.com/DearVa/Everywhere/releases/tag/v0.1.1) - 2025-07-31

### ✨ New Features
- Added Logging

### 🗑️ Removed
- Removed custom window corner radius (Too many bugs, not worth it)

### 🐞 Fixed
- Fixed I18N not working when Language is not set

**Full Changelog**: https://github.com/DearVa/Everywhere/compare/v0.1.0...v0.1.1



## [v0.1.0](https://github.com/DearVa/Everywhere/releases/tag/v0.1.0) - 2025-07-31

### First Release · 万物生于有，有生于无。
