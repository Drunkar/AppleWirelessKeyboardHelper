import clr

from System import *
from System.Diagnostics import Process
from System.Runtime.InteropServices import Marshal
from System.Windows.Forms import *
from Misuzilla.Applications.AppleWirelessKeyboardHelper import Program, Util

# Master Volume Control
clr.AddReferenceByPartialName("MasterVolumeControlLibrary")
from MasterVolumeControlLibrary import MasterVolumeControl
volControl = MasterVolumeControl.GetControl()

def OnLoad(sender, e):
  pass

def OnUnload(sender, e):
  volControl.Dispose()

Program.Load   += OnLoad
Program.Unload += OnUnload

"""
Power Button
"""
def OnDown_Power():
  # Lock desktop
  Process.Start("rundll32.exe", "user32.dll,LockWorkStation")

"""
Eject Button
"""
def OnDown_Eject():
  #Util.Eject("E");
  pass

"""
Fn + F1 ... F12 (OnDown_Fn_[KeyName])
"""
def OnDown_Fn_F1():
  MessageBox.Show('Fn+F1') # System.Windows.Forms.MessageBox

def OnDown_Fn_F2():
  Program.ShowBalloonTip('Fn+F2') # ShowBalloonTip(str) or ShowBalloonTip(str, System.Windows.Forms.ToolTipIcon)

def OnDown_Fn_F3():
  if Environment.OSVersion.Version.Major >= 6:
    Process.Start("rundll32.exe", "DwmApi #105") # 3D Filp
  else:
    toggleDesktop() # Show Desktops

def OnDown_Fn_F4():
  Util.SendInput(Keys.PrintScreen) # System.Windows.Forms.Keys

def OnDown_Fn_F5():
  pass

def OnDown_Fn_F6():
  pass

def OnDown_Fn_F7():
  # iTunes / Previous Track
  execiTunes(lambda it: it.PreviousTrack())

def OnDown_Fn_F8():
  # iTunes / PlayPause
  execiTunes(lambda it: it.PlayPause())

def OnDown_Fn_F9():
  # iTunes / PlayPause
  execiTunes(lambda it: it.NextTrack())

def OnDown_Fn_F10():
  volControl.Mute = not volControl.Mute

def OnDown_Fn_F11():
  volControl.VolumeDown()

def OnDown_Fn_F12():
  volControl.VolumeUp()

# ----

# Create COM Object
def createObject(progID):
  t = Type.GetTypeFromProgID(progID)
  return Activator.CreateInstance(t)

# iTunes Helper functions
def execiTunes(f):
  it = createObject('iTunes.Application')
  f(it)
  Marshal.ReleaseComObject(it)

# Show desktop
def toggleDesktop():
  shell = createObject('Shell.Application')
  shell.ToggleDesktop()
  Marshal.ReleaseComObject(shell)
