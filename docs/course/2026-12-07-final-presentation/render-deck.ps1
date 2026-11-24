# Exports a .pptx to PDF and per-slide PNGs with PowerPoint COM.
param([string]$Pptx, [string]$OutDir, [string]$Pdf)
$Pptx = (Resolve-Path $Pptx).Path
New-Item -ItemType Directory -Force $OutDir | Out-Null
$OutDir = (Resolve-Path $OutDir).Path
Get-ChildItem $OutDir -Filter *.png | Remove-Item
$app = New-Object -ComObject PowerPoint.Application
$p = $app.Presentations.Open($Pptx, $true, $false, $false)
$i = 0
foreach ($s in $p.Slides) { $i++; $s.Export((Join-Path $OutDir ('slide{0:D2}.png' -f $i)), 'PNG', 1920, 1080) }
if ($Pdf) { $p.SaveAs($Pdf, 32) }
$p.Close()
$app.Quit()
"$i slides"
