Public Class Form1
    Dim scale As Integer = 4
    Dim running As Boolean = True
    Dim loaded As Boolean
    Dim frameshown As Integer = 0
    Dim frameimages As New List(Of Bitmap)
    Dim commands As New List(Of Command)
    Private Sub Form1_Load(sender As Object, e As EventArgs) Handles MyBase.Load
        Me.SetStyle(ControlStyles.OptimizedDoubleBuffer Or ControlStyles.UserPaint Or ControlStyles.AllPaintingInWmPaint, True)
        Dim gameThread As New Threading.Thread(Sub()
                                                   Dim sw As New Stopwatch
                                                   sw.Start()
                                                   While running
                                                       If sw.ElapsedMilliseconds >= 1000 / 60 Then
                                                           sw.Restart()
                                                           tick
                                                       End If
                                                       Threading.Thread.Sleep(1)
                                                   End While
                                               End Sub)
        Dim graphicsThread As New Threading.Thread(Sub()
                                                       Dim sw As New Stopwatch
                                                       sw.Start()
                                                       While running
                                                           If sw.ElapsedMilliseconds >= 1000 / 60 Then
                                                               sw.Restart()
                                                               If Me.IsHandleCreated Then
                                                                   Me.BeginInvoke(New Action(AddressOf Me.Refresh))
                                                               End If
                                                           End If
                                                           Threading.Thread.Sleep(1)
                                                       End While
                                                   End Sub)
        gameThread.IsBackground = True
        graphicsThread.IsBackground = True
        gameThread.Start()
        graphicsThread.Start()
    End Sub
    Private Sub Form1_DragEnter(sender As Object, e As DragEventArgs) Handles Me.DragEnter
        If e.Data.GetDataPresent(DataFormats.FileDrop) Then
            e.Effect = DragDropEffects.Copy
        End If
    End Sub

    Private Sub Form1_DragDrop(sender As Object, e As DragEventArgs) Handles Me.DragDrop
        loaded = False
        Dim files() As String = CType(e.Data.GetData(DataFormats.FileDrop), String())
        Dim filteredFiles = files.Where(Function(f) IO.Path.GetFileName(f).StartsWith("front") Or IO.Path.GetFileName(f).StartsWith("anim")).ToArray()
        files = If(filteredFiles.Any(), filteredFiles, files).OrderBy(Function(f) IO.Path.GetExtension(f).First()).ToArray()


        For Each f As String In files
            If f.ToLower.EndsWith(".png") Then
                LoadImage(New Bitmap(f))
                commands.Clear()
            ElseIf f.ToLower.EndsWith(".asm") Then
                commands = ReadCommandsFromFile(f)
            End If
        Next
        loaded = True
        RunAnimation()
        Me.Refresh()
    End Sub
    Public Function ReadCommandsFromFile(filePath As String) As List(Of Command)
        Dim commands As New List(Of Command)
        Dim lines As String() = IO.File.ReadAllLines(filePath)

        For Each line As String In lines
            Dim cleanedLine = line.Trim().ToLower()
            Dim command As New Command
            Dim firstPart As String = cleanedLine.Split(" "c)(0).Trim()

            Select Case firstPart
                Case "frame"
                    Dim frameDetails = cleanedLine.Substring(5).Trim().Split(","c)
                    command.Type = CommandType.Frame
                    command.Param1 = CInt(frameDetails(0).Trim())  ' Frame index
                    command.Param2 = CInt(frameDetails(1).Trim())  ' Duration
                Case "setrepeat"
                    command.Type = CommandType.SetRepeat
                    command.Param1 = CInt(cleanedLine.Split(" "c)(1).Trim())  ' Repeat count
                Case "dorepeat"
                    command.Type = CommandType.DoRepeat
                    command.Param1 = CInt(cleanedLine.Split(" "c)(1).Trim())  ' Start index for repeat
                Case "endanim"
                    command.Type = CommandType.EndAnim
            End Select

            commands.Add(command)
        Next

        Return commands
    End Function
    Public Function LoadImage(img As Bitmap)
        frameimages.Clear()
        frameshown = 0
        Dim w As Integer = img.Width
        Dim c As Integer = img.Height / w
        For i = 0 To c - 1
            frameimages.Add(ImageSplicer(img, New Rectangle(0, w * i, w, w)))
        Next
    End Function
    Private Sub Form1_FormClosing(sender As Object, e As FormClosingEventArgs) Handles Me.FormClosing
        running = False
    End Sub
    Private Sub Form1_Paint(sender As Object, e As PaintEventArgs) Handles Me.Paint
        e.Graphics.CompositingQuality = Drawing2D.CompositingQuality.HighSpeed
        e.Graphics.InterpolationMode = Drawing2D.InterpolationMode.NearestNeighbor
        e.Graphics.PixelOffsetMode = Drawing2D.PixelOffsetMode.Half
        e.Graphics.ScaleTransform(scale, scale)
        e.Graphics.TranslateTransform(56, 0)
        e.Graphics.ScaleTransform(-1, 1)
        If loaded Then
            If frameimages.Count > frameshown Then
                e.Graphics.DrawImage(frameimages(frameshown), 0, 0)
            End If
        End If
    End Sub
    Public Sub tick()
        If loaded Then
            RunAnimation()
        End If
    End Sub
    Public Function ImageSplicer(src As Bitmap, r As Rectangle) As Bitmap
        Dim img As New Bitmap(r.Width, r.Height)
        Using g As Graphics = Graphics.FromImage(img)
            g.DrawImage(src, -r.X, -r.Y, src.Width, src.Height)
        End Using
        Return img
    End Function

    Public Sub RunAnimation()
        Dim i As Integer = 0
        Dim stack As New Stack(Of Tuple(Of Integer, Integer, Integer))  ' Tuple contains (start index, repeat count, current iteration)

        While i < commands.Count
            If Not loaded Then Exit Sub
            Dim cmd = commands(i)
            Select Case cmd.Type
                Case CommandType.Frame
                    DisplayFrame(cmd.Param1, cmd.Param2)
                Case CommandType.SetRepeat
                    ' Set up the next repeat context, push on stack
                    stack.Push(New Tuple(Of Integer, Integer, Integer)(i + 1, cmd.Param1, 0))
                Case CommandType.DoRepeat
                    Dim context = stack.Peek()
                    If context.Item3 < context.Item2 - 1 Then
                        ' Continue repeating
                        stack.Pop()
                        stack.Push(New Tuple(Of Integer, Integer, Integer)(context.Item1, context.Item2, context.Item3 + 1))
                        i = context.Item1 - 1  ' Jump back to the start of the loop
                    Else
                        ' End of repeat, pop from stack
                        stack.Pop()
                    End If
                Case CommandType.EndAnim
                    Exit Sub
            End Select
            i += 1
        End While
    End Sub

    Public Enum CommandType
        Frame
        SetRepeat
        DoRepeat
        EndAnim
    End Enum

    Public Structure Command
        Public Type As CommandType
        Public Param1 As Integer  ' Used for frame index, repeat count, or start index of repeat
        Public Param2 As Integer  ' Used for duration in case of frames
    End Structure


    Public Sub DisplayFrame(id As Integer, durantion As Integer)
        frameshown = id
        Threading.Thread.Sleep(durantion * 25)
    End Sub

End Class
