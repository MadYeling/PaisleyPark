## 来自译者的话
**如果你是在GitHub上搜索到此项目的话，还请看完最后这点。**
#### 请勿在任何途径传播、宣传此分支上的 Paisley Park
- 此软件完全由[LeonBlade](httPS://github.com/LeonBlade)开发，本人只是将其翻译成为中文并，有问题请找原作者。
- 本人不在任何平台宣传发布此软件，此项目基本属于疫情期间太无聊没事找事，只用于程序员朋友间交流，如果事态控制不住随时可能删除此项目。
- 目前有其它人搬运、翻译和提供国服Offset，我和他们完全不认识，他们搬运的用出事情还请不要怪到我头上来。
- 此分支代码中的Offset更新地址和软件更新地址已经修改到此分支上。
- 项目原链接：[PaisleyPark](httPS://github.com/LeonBlade/PaisleyPark)


# Paisley Park 中文介绍

Paisley Park 是一个自动标点工具，允许你保存预设的标点并且可以随时加载，免去手动标点的苦恼。

## 它是如何工作的？

Paisley Park 通过修改程序内存来工作（PS：注入程序用的代码已被废弃），但是 Paisley Park 不会对任何进程注入任何恶意代码，只注入代码用来帮助调试进程中已经存在的函数。当应用程序正常关闭后，Paisley Park 会清理它所执行的代码，就像一切都没有发生过一样。Paisley Park 不会永久的影响任何进程。如果你希望查看运行时注入的内容，可以[点击这里](httPS://github.com/Madyeling/PaisleyPark/blob/master/PaisleyPark/ViewModels/MainWindowViewModel.cs#L213)。

## 我用这个会有麻烦吗？

Paisley Park 是通过逆向工程修改运行中程序的内存来实现的（PS：类似于CE），这意味着使用 Paisley Park 是违反服务条款的。然而，我个人认为这个工具处于灰色地带，类似于其它第三方应用程序例如ACT，它不会对任何人的体验产生负面的影响，也不会给你带来任何比其他人更大的优势，同时也不是以任何方式进行作弊。这只是一个工具，在我看来它只是自动化执行了一个现有特性。（PS：然后这个特性就在5.2被吉田制裁了...被发现使用此软件肯定会有麻烦找上门，请自行斟酌）

## 这是个病毒吗？

Paisley Park 100% 安全！ 如果你有所怀疑的话，可以翻看整个项目的源代码，也许你的杀毒软件会标记此软件为病毒，因为 Paisley Park 确实修改了其它正在运行的应用程序的内存，有些杀毒软件确实把这种行为定义成病毒，但是请注意，杀毒软件并不是永远正确的，如果你还是抱有疑问或担忧，我再次要求你自行查看源代码。（PS：这东西就是个自动化的CE，懂我意思了吧？）

## 它会把我的游戏搞崩溃吗？

Paisley Park 也许会把你的游戏搞崩溃，理论上这只会发生在游戏版本更新之后。我目前正在努力防止这种事情发生，但是需要之后的版本才会实现。这意味着当游戏刚更新时 Paisley Park 可能短时间内无法使用，直到它更新。但是其他的错误仍有可能会发生，如果游戏崩溃了，请将目录下的 `error.log` 和 `output.log` 提交给我，我会尽力帮你。（PS：汉化版的log被我手贱翻译了一部分，就不要去找作者了）

## 怎么使用此软件？

从我最初的设想来看， Paisely Park 还处在非常基本的水平. 一些附加的功能正在开发中，但是由于缺乏空闲时间，目前进展缓慢。也就是说，目前 Paisley Park 的功能是齐全的，而且相当便于使用，当第你第一次启动应用程序的时候，点击设置按钮。（PS：嗯？哪儿有设置按钮？？）

### 加载

在主窗口中，你将在这里加载预设，只需要从列表中选择一个预设标点，然后单击加载，游戏中就会立刻出现标点，这些标点不仅会出现在你的游戏中，别人也能看见，在不认识的人面前使用时请小心（PS：5.2之后别人看不见了）

### 创建

在创建之前，你需要在游戏中放置标点到你想要的位置，然后点击主页面上三个点的按钮，打开管理器，之后点击创建，为预设内容命名，之后确保勾选“使用当前标点坐标”，然后单击创建。这将会向列表中添加一个新的预设。之后在主窗口中你就可以在下拉菜单中找到你的新预设，并且随时点击“加载”来加载它。

### 编辑

编辑功能就像创建，除了它只能用在现有的预设上。如果未勾选“使用当前标点坐标”，您将只更新预设的名称。

### 删除

删除预设很简单，只需要在预设管理器中选中预设，单击“删除”即可，在删除之前 Paisley Park 会询问你是否真的要删除，以确保你不是按错了。

### 导入/导出

Paisley Park 设计的初衷是为了方便在各种场合中放置标点，考虑到这一点它应该具有导入导出标点配置的功能，以便于和其他人分享标点坐标。导入前你需要将其他人的坐标JSON字符串复制下来，然后单击“导入”，它就会出现在你的预设列表内。导出也是一样的，只需选中你想要分享的标点预设，点击“导出”，之后就可以将它粘贴到你想要的地方。虽然复制粘贴这些JOSN字符串并不违反服务条款，但是安全起见，最好不要在游戏中分享。

## 最后

感谢你抽出时间来观看这个项目，我希望它对你的零式之夜或者是其它使用它的理由有所帮助。如果你有任何的建议，请在这个GitHub页面上上留下“Issues”，或者使用Discord联系：LeonBlade#9988。（PS：请去[原作者](httPS://github.com/LeonBlade/PaisleyPark)的GitHub留Issues，用英文！）

你也许已经知道我是"SSTool"的原作者，这是一个最终幻想XIV的截图工具；或是CE脚本"Tabletopper"的原作者，这个脚本能让任何东西都当做桌台物品，以获得更好的装修体验（PS：嗯？鲶鱼精原型？）。我花费了大量的精力来为他人创造这些工具，并且我计划着在未来创造更多。对于到目前为止支持我的人，我非常感谢你们。

如果你出于任何原因想捐款给我，我非常感激。不过，无论是否有金钱上的支持，我们都非常感激。知道人们在使用我的工具并享受它们让我很开心。

httPS://ko-fi.com/leonblade


# 以下是英文原文

# Paisley Park

Paisley Park is a waymark preset tool that allows you to save and load waymark presets to be used at any time without needing to do all the work manually.

## How does it work?

Paisley Park works by injecting assembly into the running application. Paisley Park however does not perform any malicious code on any process, and only injects code to assist with calling functions that already exist inside of the process. When the application is shut down properly, Paisley Park cleans up its mess as if nothing happened. Nothing Paisley Park does affects any process permanently. If you wish to see what is injected at runtime, you can view so [here](httPS://github.com/LeonBlade/PaisleyPark/blob/master/PaisleyPark/ViewModels/MainWindowViewModel.cs#L213).

## Will I get in trouble for using this?

Paisley Park was created by means of reverse engineering and is implemented through means of modifying the memory of the running process. This means that using this tool is against the terms of service. However, it is my opinion that this tool sits in the gray area that other similar third party applications such as ACT are where they don't negatively impact anyone experience, nor does it give you any major advantage over others or cheat in any way. This is simply a tool which automates a process that (in my opinion) should already be an existing feature.

## Is this a virus?

Paisley Park is 100% safe! The entire source code of this project is available for you to look through if you're skeptical. Your antivirus software may trigger a false positive. Paisley Park does modify the memory of a running application. This may be seen as a number of different types of viruses at a rudementary level. Please note that virus scanning software isn't always completely thourough, and I again ask you to look over the code for yourself if you have any doubts or concerns.

## Will it crash my game?

There is a likelyhood that Paisley Park could crash your game. However, this should only happen currently when the game is updated. Efforts are being made to prevent this from happening, but won't be available until a future update. This means that you cannot use Paisley Park on a new update of the game until it's updated for the patch. Other crashes are still possible however. In the event this happens, please submit an Issue here with your `error.log` and `output.log` located in the application folder and I'll try to assist you the best I can.

## How do I use it?

Paisely Park is currently at a very basic level from how I initially envisioned it. There are additional features that are in the pipeline, but cannot be worked on right now due to a lack of free time. That being said, Paisley Park is fully functional in its current state and is very easy to use. When starting up the application for the first time, click on the Settings button.

### Load

On the main window, this is where you will load your presets. Simply select one of the presets from the list and click load. Instantly, the waymarks will appear in game. These waymarks don't only show up on your screen, but for everyone in your party as well. Please take note of this before using it with people you don't know.

### Create

To create a new preset, simply place waymarks down in game where you wish to save them. Then, click create and name your preset something memorable. Ensure that the "Use current waymarks" is checked and click Create. This will add a new preset from your list. Now, on the main window, you can select your new preset from the drop down and click "Load" at any time to load this preset.

### Edit

Edit functions just like create, except it works on existing presets. Leaving the "Use current waymarks" unchecked, you will only update the name of the preset.

### Delete

Deleting is as simple as selecting a preset to delete, and clicking delete. Paisely Park will ask you first if you wish to delete to ensure you don't make any unwanted mistakes.

### Import/Export

Paisley Park was created for the purpose of making it easy to place waymarks down for various raid scenarios. With that in mind, it makes perfect sense that Paisely Park should have a feature to import and export presets to share with other members of the community. Simply click "Import" and paste in the JSON string shared from another user and click to import it. This will add that preset to your list. The same can be done for sharing your own. Simply click on which preset you wish to share, and click "Export". This will copy the JSON string to your clipboard where you can paste it in Discord, Reddit or elsewhere. While pasting these JSON strings aren't against the terms of service, it might be in your best interest not to share them in game if you wish to be safe.

## Final

Thank you for taking the time to view this project. I hope that you find it useful for your raid nights, or whatever else you find reason to use it. If you have any suggestions, feel free to leave them as "Issues" on this GitHub page, or message me on Discord: LeonBlade#9988.

You may already know of me as the original creator of what is now referred to as "SSTool", the screenshot tool for FFXIV, or from my Cheat Engine script "Tabletopper" to treat any item as a tabletop item for more housing options. I put a lot of effort into creating these tools for others, and I plan on creating more in the future. For those of you who have supported me so far, I thank you very much.

If you wish to donate to me for any reason, I greatly appreciate it. Any support though, monetarily or not is greatly appreciated. Knowing that people are using my tools and enjoying them makes me happy.

httPS://ko-fi.com/leonblade
