$godot = "$PSScriptRoot/../Tools/Godot/Godot_v4.5.1-stable_mono_win64\Godot_v4.5.1-stable_mono_win64_console.exe"
$project = $PSScriptRoot
& $godot --path $project CoilStoryFlight.tscn $args
