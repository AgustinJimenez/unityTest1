param(
    [Parameter(Mandatory=$true)]
    [string]$Text
)

Add-Type -AssemblyName System.Speech
$synth = New-Object System.Speech.Synthesis.SpeechSynthesizer
$synth.Speak($Text)
