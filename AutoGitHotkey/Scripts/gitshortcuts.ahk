^SC046::applyShortcut("stash")
+^SC046::applyShortcut("pop_and_ask_for_anyway")
!+^SC046::applyShortcut("pop_anyway")
!SC046::applyShortcut("stash_staged")
!^SC046::applyShortcut("stash_unstaged")
!+SC046::applyShortcut("amend_stash")


applyShortcut(arg)
{
	Run, D:\AutoGitHotkey\AutoGitHotkey\bin\x86\Debug\JBSnorro.AutoGitHotkey.exe %arg%
}
