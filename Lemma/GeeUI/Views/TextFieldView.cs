﻿
using System;
using System.Text.RegularExpressions;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using GeeUI.Structs;
using GeeUI.Managers;
using Keys = Microsoft.Xna.Framework.Input.Keys;

namespace GeeUI.Views
{
	public class TextFieldView : View
	{

		public static Color DefaultSelectionColor = new Color(13.0f / 255.0f, 103.0f / 255.0f, 172.0f / 255.0f);
		public override Rectangle ContentBoundBox
		{
			get
			{
				return new Rectangle(RealX, RealY, Width, Height);
			}
		}

		public override Rectangle BoundBox
		{
			get
			{
				var patch = Selected ? NinePatchSelected : NinePatchDefault;
				return new Rectangle(RealX - patch.LeftWidth, RealY - patch.TopHeight, Width + patch.LeftWidth + patch.RightWidth, Height + patch.TopHeight + patch.BottomHeight);
			}
		}

		public NinePatch NinePatchDefault;
		public NinePatch NinePatchSelected;
		public NinePatch NinePatchRegexGood;
		public NinePatch NinePatchRegexBad;

		public bool AllowTab = true;

		public Color TextColor;

		public bool MultiLine = true;
		public bool Editable = true;

		private string _text = "";

		//public Property<string> Text;
		public string Text
		{
			get
			{
				return MultiLine ? _text : _text.Replace("\n", "");
			}
			set
			{
				bool call = _text != value;
				_text = value;
				if (call) CallOnChanged();
			}
		}

		public int ShownWidth = 0;

		public bool SubmitOnClickAway = true;

		//Avoid infinite recursion
		private bool _callingOnChanged = false;

		public Action OnTextChanged = null;
		public Action OnTextSubmitted = null;

		private int _offsetX;
		private int _offsetY;
		private int _cursorX;
		private int _cursorY;

		private Vector2 _selectionStart = new Vector2(-1);
		private Vector2 _selectionEnd = new Vector2(-1);

		private float _buttonHeldTime;

		//How long to press before repeating
		private float _buttonHeldTimePreRepeat = 2;
		private string _buttonHeldString = "";
		private Keys _buttonHeld = Keys.None;

		float _delimiterTime;
		private const float DelimiterLimit = 0.7f;
		bool _doingDelimiter;

		private float _dragOffInterval = 0.015f;
		private float _dragOffTimer = 0f;

		public string OffsetText
		{
			get
			{
				var ret = "";
				var lines = TextLines;
				var allowedWidth = Width;
				var allowedHeight = Height;
				for (var iY = _offsetY; iY < lines.Length; iY++)
				{
					var curLine = lines[iY];
					var curLineRet = "";
					for (int iX = _offsetX; iX < curLine.Length; iX++)
					{
						var lineWidth = (int)GeeUIMain.Font.MeasureString(curLineRet + curLine[iX]).X;
						if (lineWidth >= allowedWidth)
						{
							break;
						}
						curLineRet += curLine[iX];
					}
					ShownWidth = curLineRet.Length;
					var retTest = ret + curLineRet + (iY + 1 != lines.Length ? "\n" : "");
					var maxHeight = (int)GeeUIMain.Font.MeasureString(retTest).Y;
					ret += curLineRet + (iY + 1 != lines.Length ? "\n" : "");
					if (maxHeight >= allowedHeight)
						break;
				}
				return ret;
			}
		}

		public string[] TextLines
		{
			get
			{
				return Text.Split('\n');
			}
			set
			{
				string cur = "";
				for (int i = 0; i < value.Length; i++)
				{
					cur += value[i];
					if (i < value.Length - 1)
						cur += "\n";
				}
				Text = cur;
			}
		}

		private string _validationRegexString = "";
		private Regex _validationRegex;

		public string ValidationRegex
		{
			get { return _validationRegexString; }
			set
			{
				_validationRegexString = value;
				_validationRegex = new Regex(value);
			}
		}

		public Func<string, bool> Validator;

		public TextFieldView(GeeUIMain GeeUI, View rootView, Vector2 position)
			: base(GeeUI, rootView)
		{
			NinePatchDefault = GeeUIMain.NinePatchTextFieldDefault;
			NinePatchSelected = GeeUIMain.NinePatchTextFieldSelected;
			NinePatchRegexGood = GeeUIMain.NinePatchTextFieldRight;
			NinePatchRegexBad = GeeUIMain.NinePatchTextFieldWrong;

			Position.Value = position;

			GeeUI.OnKeyPressedHandler += keyPressedHandler;
			GeeUI.OnKeyReleasedHandler += keyReleasedHandler;

			TextColor = GeeUI.TextColorDefault;

			ContentMustBeScissored.Value = true;

			//Text = new Property<string>();
			//Text.Get = () => MultiLine ? Text.Value : Text.Value.Replace("\n", "");
		}

		void keyReleasedHandler(string keyReleased, Keys key)
		{
			if (_buttonHeld != key) return;
			_buttonHeldTime = 0;
			_buttonHeld = Keys.None;
			_buttonHeldTimePreRepeat = 0;
			_buttonHeldString = "";
		}

		private bool justReceivedFocus;
		public override void Focus()
		{
			base.Focus();
			this.justReceivedFocus = true;
		}

		void keyPressedHandler(string keyPressed, Keys key)
		{
			if (!Selected || !Editable) return;
			if (_buttonHeld != key)
			{
				_buttonHeld = key;
				_buttonHeldTime = 0;
				_buttonHeldTimePreRepeat = 0;
				_buttonHeldString = keyPressed;
			}
			bool ctrlPressed = InputManager.IsKeyPressed(Keys.LeftControl) || InputManager.IsKeyPressed(Keys.RightControl);

			if (ctrlPressed)
			{
				switch (key)
				{
					case Keys.A:
						this._selectionStart = new Vector2(0, 0);
						this._selectionEnd = new Vector2(TextLines[TextLines.Length - 1].Length, TextLines.Length - 1);
						break;

					case Keys.C:
						break;

					case Keys.X:
						break;

					case Keys.V:
#if FNA
						string text = SDL2.SDL.SDL_GetClipboardText();
#else
						string text = System.Windows.Forms.Clipboard.GetText();
#endif
						if (!String.IsNullOrEmpty(text))
							AppendTextCursor(text);
						break;

					case Keys.Back:
						break;
				}
			}
			else
			{
				switch (key)
				{
					case Keys.Back:
						erase(false);
						break;
					case Keys.Delete:
						erase(true);
						break;
					case Keys.Left:
						SelectionArrowKeys();
						MoveCursorX(-1);
						break;
					case Keys.Right:
						SelectionArrowKeys();
						MoveCursorX(1);
						break;
					case Keys.Up:
						SelectionArrowKeys();
						MoveCursorY(-1);
						break;
					case Keys.Down:
						SelectionArrowKeys();
						MoveCursorY(1);
						break;
					case Keys.Enter:
						if (MultiLine)
							AppendTextCursor("\n");
						else if (Text.Length != 0 && OnTextSubmitted != null && this.Validate())
							OnTextSubmitted();
						break;
					case Keys.Space:
						AppendTextCursor(" ");
						break;
					case Keys.Tab:
						if (InputManager.IsKeyPressed(Keys.LeftControl))
							this.Blur();

						if (this.AllowTab && !this.justReceivedFocus)
							this.FocusNextInputView(reverse: InputManager.IsKeyPressed(Keys.LeftShift));
						break;
					default:
						if (this.Filter(key))
							AppendTextCursor(keyPressed);
						break;
				}
			}
			ReEvaluateOffset();
		}

		private void SelectionArrowKeys()
		{
			if (_selectionEnd == _selectionStart || _selectionEnd == new Vector2(-1)) return;

			var start = _selectionStart;
			var end = _selectionEnd;
			if (_selectionStart.Y > _selectionEnd.Y || (_selectionStart.Y == _selectionEnd.Y && _selectionStart.X > _selectionEnd.X))
			{
				//Need to swap the variables.
				var store = start;
				start = end;
				end = store;
			}
			_cursorX = (int)end.X;
			_cursorY = (int)end.Y;
			_selectionEnd = _selectionStart = new Vector2(-1);
		}

		private void erase(bool forward)
		{
			if (_selectionStart == _selectionEnd || _selectionEnd == new Vector2(-1))
			{
				var lines = TextLines;
				var curPos = _cursorX;
				for (var i = 0; i < _cursorY; i++)
				{
					var lineL = lines[i] + (i < _cursorY ? "\n" : "");
					curPos += lineL.Length;
				}

				if (forward && curPos < Text.Length)
					Text = Text.Remove(curPos, 1);
				else if (!forward && curPos > 0)
				{
					Text = Text.Remove(curPos - 1, 1);
					_cursorX--;
				}

				if (_cursorX < 0)
				{
					_cursorX = lines[_cursorY - 1].Length;
					_cursorY--;
				}
				return;
			}


			var start = _selectionStart;
			var end = _selectionEnd;
			if (_selectionStart.Y > _selectionEnd.Y || (_selectionStart.Y == _selectionEnd.Y && _selectionStart.X > _selectionEnd.X))
			{
				//Need to swap the variables.
				var store = start;
				start = end;
				end = store;
			}
			string before = "";
			string after = "";

			float beforeEndX = start.X;
			float beforeEndY = start.Y;
			float afterStartX = end.X;
			float afterStartY = end.Y;

			for (int y = 0; y <= beforeEndY; y++)
			{
				if (y != 0) before += "\n";
				var xUnder = (y == beforeEndY) ? beforeEndX : TextLines[y].Length;
				for (int x = 0; x < xUnder; x++)
				{
					before += TextLines[y][x];
				}
			}
			for (var y = afterStartY; y < TextLines.Length; y++)
			{
				if (y != afterStartY) after += "\n";
				for (var x = afterStartX; x < TextLines[(int)y].Length; x++)
				{
					after += TextLines[(int)y][(int)x];
				}
				afterStartX = 0;
			}
			Text = before;
			//Hacky way of easily finding the co-ordinates for the new cursor position.
			_cursorY = TextLines.Length - 1;
			_cursorX = TextLines[_cursorY].Length;
			Text += after;

			_selectionEnd = _selectionStart = new Vector2(-1);
		}

		private void MoveCursorX(int xMovement)
		{
			string[] lines = TextLines;
			_cursorX += xMovement;
			if (_cursorX < 0)
			{
				int yMinus = _cursorY - 1;
				if (yMinus < 0)
				{
					_cursorX = 0;
				}
				else
				{
					string line = lines[yMinus];
					_cursorX = line.Length;
					_cursorY = yMinus;
				}
			}
			else if (_cursorX > lines[_cursorY].Length)
			{
				if (_cursorY < lines.Length - 1)
				{
					_cursorY++;
					_cursorX = 0;
				}
				else
				{
					_cursorX = lines[_cursorY].Length;
				}
			}

			ReEvaluateOffset();
		}

		private void MoveCursorY(int yMovement)
		{
			string[] lines = TextLines;
			_cursorY += yMovement;
			if (_cursorY >= lines.Length) _cursorY = lines.Length - 1;
			else if (_cursorY < 0) _cursorY = 0;
			string line = lines[_cursorY];
			if (_cursorX >= line.Length) _cursorX = line.Length;

			ReEvaluateOffset();
		}

		private void MoveOffsetX(int xMovement, bool moveSelection)
		{
			if (_offsetX + xMovement > (TextLines[_cursorY].Length - ShownWidth)) xMovement = (TextLines[_cursorY].Length - ShownWidth) - _offsetX;
			if (_offsetX + xMovement < 0) xMovement = -_offsetX;
			_offsetX += xMovement;
			if (moveSelection)
			{
				_selectionEnd.X += xMovement;
			}
		}

		private void ReEvaluateOffset()
		{
			if (_selectionStart == _selectionEnd)
				_selectionEnd = _selectionStart = Vector2.Zero;
			var ret = "";
			var lines = TextLines;
			var allowedWidth = Width;
			var allowedHeight = Height;

			var maxCharX = 0;
			var maxCharY = 0;

			var xDiff = _cursorX - _offsetX;
			var yDiff = _cursorY - _offsetY;

			if (xDiff < 0) _offsetX += xDiff;
			if (yDiff < 0) _offsetY += yDiff;

			for (var iY = _offsetY; iY < lines.Length; iY++)
			{
				var curLine = lines[iY];
				var curLineRet = "";
				for (var iX = _offsetX; iX < curLine.Length; iX++)
				{
					var lineWidth = (int)GeeUIMain.Font.MeasureString(curLineRet + curLine[iX]).X;
					if (lineWidth >= allowedWidth)
					{
						break;
					}
					curLineRet += curLine[iX];
					if (iY == _cursorY)
						maxCharX++;
				}
				ret += curLineRet + (iY + 1 != lines.Length ? "\n" : "");
				var lineHeight = (int)GeeUIMain.Font.MeasureString(ret).Y;
				if (lineHeight >= allowedHeight)
				{
					break;
				}
				maxCharY++;
			}
			if (maxCharX < xDiff)
				_offsetX += xDiff - maxCharX;
			if (maxCharY < yDiff) _offsetY++;
		}

		public Func<Keys, bool> Filter = x => true;

		private void AppendTextCursor(string text)
		{
			string before = "";
			string after = "";

			float beforeEndX = _cursorX;
			float beforeEndY = _cursorY;
			float afterStartX = _cursorX;
			float afterStartY = _cursorY;

			if (_selectionStart != _selectionEnd && _selectionEnd != new Vector2(-1))
			{
				var start = _selectionStart;
				var end = _selectionEnd;
				if (_selectionStart.Y > _selectionEnd.Y || (_selectionStart.Y == _selectionEnd.Y && _selectionStart.X > _selectionEnd.X))
				{
					//Need to swap the variables.
					var store = start;
					start = end;
					end = store;
				}

				beforeEndX = start.X;
				beforeEndY = start.Y;
				afterStartX = end.X;
				afterStartY = end.Y;
			}
			for (int y = 0; y <= beforeEndY; y++)
			{
				if (y != 0) before += "\n";
				var xUnder = (y == beforeEndY) ? beforeEndX : TextLines[y].Length;
				for (int x = 0; x < xUnder && x < TextLines[y].Length; x++)
				{
					before += TextLines[y][x];
				}
			}
			for (var y = afterStartY; y < TextLines.Length; y++)
			{
				if (y != afterStartY) after += "\n";
				for (var x = afterStartX; x < TextLines[(int)y].Length; x++)
				{
					after += TextLines[(int)y][(int)x];
				}
				afterStartX = 0;
			}
			Text = before + text;
			//Hacky way of easily finding the co-ordinates for the new cursor position.
			_cursorY = TextLines.Length - 1;
			_cursorX = TextLines[_cursorY].Length;
			Text += after;

			_selectionEnd = _selectionStart = new Vector2(-1);
		}

		public void AppendText(string text)
		{
			Text += text;
		}

		private void CallOnChanged()
		{
			if (OnTextChanged == null || _callingOnChanged) return;
			_callingOnChanged = true;
			OnTextChanged();
			_callingOnChanged = false;
		}

		public void SetCursorPos(int x, int y)
		{
			_cursorX = x;
			_cursorY = y;
			ReEvaluateOffset();
		}

		public bool Validate()
		{
			if (!MustValidate()) return true;
			return this.Validator != null ? this.Validator(this.Text) : _validationRegex.IsMatch(Text);
		}

		public bool MustValidate()
		{
			return this.Validator != null || !(ValidationRegex == "" || _validationRegex == null);
		}

		public Vector2 GetMouseTextPos(Vector2 pos)
		{
			var lines = TextLines;

			var patch = Selected ? NinePatchSelected : NinePatchDefault;

			var topLeftContentPos = AbsolutePosition + new Vector2(patch.LeftWidth, patch.TopHeight);
			var actualClickPos = pos - topLeftContentPos;

			var ret = new Vector2();

			var actualText = "";

			bool setY = false;
			bool setX = false;
			for (var iY = _offsetY; iY < lines.Length; iY++)
			{
				var textHeight = (int)GeeUIMain.Font.MeasureString(actualText + lines[iY]).Y;
				if (textHeight >= actualClickPos.Y)
				{
					ret.Y = iY;
					setY = true;

					var line = lines[iY];

					//No need to make another variable
					actualText = "";

					for (int iX = _offsetX; iX < line.Length; iX++)
					{
						actualText += line[iX];
						var textWidth = (int)GeeUIMain.Font.MeasureString(actualText).X;
						if (textWidth < actualClickPos.X) continue;
						ret.X = iX;
						setX = true;
						break;
					}



					break;
				}
				actualText += lines[iY] + "\n";
			}
			if (!setY) ret.Y = TextLines.Length - 1;
			if (!setX)
				ret.X = TextLines[(int)ret.Y].Length;
			return ret;
		}

		public override void OnMScroll(Vector2 position, int scrollDelta, bool fromChild = false)
		{
			if ((!Selected && !MouseOver) || !MultiLine)
			{
				base.OnMScroll(position, scrollDelta, fromChild);
				return;
			}
			int origOffset = _offsetY;
			int numLines = OffsetText.Split('\n').Length;
			_offsetY -= scrollDelta;
			if (_offsetY < 0) _offsetY = 0;
			if (_offsetY >= TextLines.Length) _offsetY = TextLines.Length - 1;
			int newLines = OffsetText.Split('\n').Length;
			if (newLines < numLines && _offsetY > origOffset)
			{
				_offsetY = origOffset;
			}
		}

		public override void OnMClick(Vector2 mousePosition, bool fromChild)
		{
			Selected.Value = true;

			var clickPos = GetMouseTextPos(mousePosition);
			_cursorX = (int)clickPos.X;
			_cursorY = (int)clickPos.Y;

			_selectionStart = clickPos;

			base.OnMClick(mousePosition, fromChild);
		}

		public override void OnMClickAway()
		{
			bool oldSelected = Selected.Value;
			Selected.Value = false;
			_selectionEnd = _selectionStart = new Vector2(-1);
			if (OnTextSubmitted != null && oldSelected && !Selected.Value && SubmitOnClickAway && this.Validate())
				OnTextSubmitted();
			base.OnMClickAway();
		}

		private Vector2 GetDrawPosForCursorPos(int cursorX, int cursorY)
		{
			var patch = Selected ? NinePatchSelected : NinePatchDefault;
			var lines = TextLines;

			var totalLine = "";
			for (int i = _offsetY; i < cursorY && i < lines.Length; i++)
			{
				var line = lines[i];
				var addNewline = (i < cursorY - 1) || (i == cursorY && line.Length == 0);
				var addSpace = (line.Length == 0);
				line += (addNewline ? "\n" : "");
				line += (addSpace ? " " : "");
				totalLine += line;
			}

			var yDrawPos = (int)(AbsoluteY + patch.TopHeight + GeeUIMain.Font.MeasureString(totalLine).Y);
			var yDrawLine = lines[cursorY];
			var cur = "";
			for (var x = _offsetX; x < cursorX && x < yDrawLine.Length; x++)
				cur += yDrawLine[x];
			var xDrawPos = (int)GeeUIMain.Font.MeasureString(cur).X + (AbsoluteX + patch.LeftWidth);

			return new Vector2(xDrawPos, yDrawPos);
		}

		public void ClearText()
		{
			this.Text = "";
			this._cursorX = 0;
			this._cursorY = 0;
			this._offsetX = 0;
			this._offsetY = 0;
		}

		public override void Update(float dt)
		{
			Vector2 pos = InputManager.GetMousePosV();
			if (Selected)
			{
				if (InputManager.IsMousePressed(MouseButton.Left))
				{
					if (!AbsoluteBoundBox.Contains(InputManager.GetMousePos()))
					{
						_dragOffTimer += dt;
						if (_dragOffTimer >= _dragOffInterval)
						{
							_dragOffTimer -= _dragOffInterval;
							if (pos.X > AbsoluteBoundBox.Right)
							{
								MoveOffsetX(1, true);
							}
							else if (pos.X < AbsoluteBoundBox.Left)
							{
								MoveOffsetX(-1, true);
							}
						}
						
					}

					var clickPos = GetMouseTextPos(InputManager.GetMousePosV());
					_selectionEnd = clickPos;
					_cursorX = (int)clickPos.X;
					_cursorY = (int)clickPos.Y;
				}

				if (InputManager.IsKeyPressed(_buttonHeld))
				{
					_buttonHeldTimePreRepeat += dt;
					_buttonHeldTime += dt;
					if (_buttonHeldTimePreRepeat >= 0.35 && _buttonHeldTime >= 0.03)
					{
						_buttonHeldTime = 0;
						keyPressedHandler(_buttonHeldString, _buttonHeld);
					}
				}
				else
				{
					keyReleasedHandler("", _buttonHeld);
				}

				_delimiterTime += dt;
				if (_delimiterTime >= DelimiterLimit)
				{
					_doingDelimiter = !_doingDelimiter;
					_delimiterTime = 0;
				}
				this.justReceivedFocus = false;
			}

			base.Update(dt);
		}

		public override void Draw(SpriteBatch spriteBatch)
		{
			var drawPos = GetDrawPosForCursorPos(_cursorX, _cursorY);
			var xDrawPos = drawPos.X;
			var yDrawPos = drawPos.Y;

			var patch = Selected ? NinePatchSelected : NinePatchDefault;
			if (MustValidate() && Text.Length > 0)
			{
				patch = Selected ? (Validate() ? NinePatchRegexGood : NinePatchRegexBad) : patch;
			}
			patch.Draw(spriteBatch, AbsolutePosition, Width, Height, 0f, EffectiveOpacity);


			base.Draw(spriteBatch);
		}

		public override void DrawContent(SpriteBatch spriteBatch)
		{
			var drawPos = GetDrawPosForCursorPos(_cursorX, _cursorY);
			var xDrawPos = drawPos.X;
			var yDrawPos = drawPos.Y;

			var patch = Selected ? NinePatchSelected : NinePatchDefault;
			if (MustValidate() && Text.Length > 0)
			{
				patch = Selected ? (Validate() ? NinePatchRegexGood : NinePatchRegexBad) : patch;
			}
			if (_selectionStart != _selectionEnd && _selectionEnd != new Vector2(-1))
			{
				var start = _selectionStart;
				var end = _selectionEnd;
				if (_selectionStart.Y > _selectionEnd.Y || (_selectionStart.Y == _selectionEnd.Y && _selectionStart.X > _selectionEnd.X))
				{
					//Need to swap the variables.
					var store = start;
					start = end;
					end = store;
				}

				for (int y = (int)Math.Max(0, start.Y); y <= Math.Min(end.Y, TextLines.Length - 1); y++)
				{
					string line = TextLines[y];
					if (line == "") line = " ";
					var startDrawX = patch.LeftWidth;
					var startDrawY = GetDrawPosForCursorPos(0, y).Y;
					var endDrawX = Width - patch.RightWidth;
					var endDrawY = GeeUIMain.Font.MeasureString(line).Y + startDrawY - 1;
					startDrawX += AbsoluteX;
					endDrawX += AbsoluteX;

					if (y == start.Y)
					{
						startDrawX = (int)GetDrawPosForCursorPos((int)start.X, (int)start.Y).X;
					}
					if (y == end.Y)
					{
						endDrawX = (int)GetDrawPosForCursorPos((int)end.X, (int)end.Y).X;
					}
					if (y == 0) endDrawY += 1;

					DrawManager.DrawBox(new Vector2(startDrawX, startDrawY), new Vector2(endDrawX, endDrawY), DefaultSelectionColor * EffectiveOpacity, spriteBatch, 0f, 20);
				}
			}

			spriteBatch.DrawString(GeeUIMain.Font, OffsetText, AbsolutePosition + new Vector2(patch.LeftWidth, patch.TopHeight), TextColor * EffectiveOpacity);

			if (_doingDelimiter && Selected && _selectionEnd == _selectionStart && Editable)
			{
				float height = GeeUIMain.Font.MeasureString("|").Y;
				DrawManager.DrawBox(new Vector2(xDrawPos, yDrawPos), new Vector2(xDrawPos + 1, yDrawPos + height), TextColor * EffectiveOpacity, spriteBatch  );
				//spriteBatch.DrawString(GeeUIMain.Font, "|", new Vector2(xDrawPos - 1, yDrawPos), TextColor * EffectiveOpacity);
			}
			base.DrawContent(spriteBatch);
		}

		public override void OnDelete()
		{
			ParentGeeUI.OnKeyPressedHandler -= keyPressedHandler;
			ParentGeeUI.OnKeyReleasedHandler -= keyReleasedHandler;
			base.OnDelete();
		}
	}
}
