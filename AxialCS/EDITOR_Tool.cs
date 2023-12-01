using Godot;
using System.Collections.Generic;
using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Threading;
using EditorTools;
using GodotPlugins.Game;

namespace AxialCS
{
	[Tool]
	public partial class EDITOR_Tool : Control
	{
		[Export]
		bool _disableScript = false, _disableInput = false, _disableDraw = false;

		[Export]
		float sideLength
		{
			get { return _sideLength; }
			set
			{
				if (OnSideLengthChanged(_sideLength, value))
					_sideLength = value;
			}
		}
		float _sideLength = 50.0f;

		[Export]
		int gridSize
		{
			get {return _gridSize; }
			set{
				_gridSize = value;
				TestAxialGrid(this, _gridSize);
			}
		}
		int _gridSize = 2;

		[Export]
		Vector2 _offset = new Vector2(1280 / 2, 720 / 2);
		static float _HEX_IMG_SCALE = 256.0f;

		// Called when the node enters the scene tree for the first time.
		public override void _Ready()
		{
			// Execute in EDITOR only
			if (Engine.IsEditorHint() && !_disableScript)
			{
				GD.Print("Executing _Ready");
			}
		}

		public void TestAxialGridProgress(Axial[] GridProgress){
			HexAxialGrid = AxialGrid.CalcHexAxialGrid(GridProgress, _offset, _sideLength);
			QueueRedraw();
		}

		private async void TestAxialGrid(EDITOR_Tool This, int gridSize)
		{
			AxialGrid axialGrid = new AxialGrid(This, gridSize);

			await Task.Run(() =>
			{
				GD.Print("Executing before wait");

				int failsafe = 0;
				while (!axialGrid.isBuilt && failsafe < 100)
				{
					Thread.Sleep(50);
					failsafe++;
				}

				Thread.Sleep(1000);
			});
			
			GD.Print("Executing after wait");
			HexAxialGrid = AxialGrid.CalcHexAxialGrid(axialGrid.Axials, _offset, _sideLength);
			GD.Print($"TOTAL HEXES: {HexAxialGrid.Count}");
		}

		// Called every frame. 'delta' is the elapsed time since the previous frame.
		public override void _Process(double delta)
		{
			// Execute in EDITOR only
			if (Engine.IsEditorHint() && !_disableScript)
			{
				// ...
				if (!_disableInput)
					ProcessInput();
			}
		}

		private static int _POINTS_LENGTH = 100;
		private Vector2[] _pxPoints = System.Linq.Enumerable.Repeat(Vector2.Zero, _POINTS_LENGTH).ToArray();
		private Axial[] _axPoints = System.Linq.Enumerable.Repeat(Axial.Empty, _POINTS_LENGTH).ToArray();
		private HexagonDraw[] _hexDraws = System.Linq.Enumerable.Repeat(HexagonDraw.Empty, _POINTS_LENGTH).ToArray();
		private HexagonDraw[] HexDrawsUsed => _hexDraws.Where(h => h != HexagonDraw.Empty).ToArray();
		private Axial _axMousePosition = Axial.Empty;

		private static int _GRID_STEPS = 100;

		private Dictionary<Axial, HexagonDraw> HexAxialGrid = new Dictionary<Axial, HexagonDraw>();

		public override void _Draw()
		{
			if(_disableDraw)
				return;

			for (int i = 0; i < _pxPoints.Length; i++)
			{
				Vector2 pxPoint = _pxPoints[i];
				Axial axPoint = _axPoints[i];
				HexagonDraw hexDraw = _hexDraws[i];
				Vector2 axPx = Axial.AxToPx(_offset, _sideLength, axPoint);

				if (pxPoint != Vector2.Zero)
				{
					DrawCircle(pxPoint, 2.0f, Colors.Red);
				}
				if (axPx != Vector2.Zero)
				{
					DrawCircle(axPx, 2.0f, Colors.Green);
				}
				if (hexDraw != HexagonDraw.Zero)
				{
					DrawPolygon(hexDraw.Vertices, hexDraw.Colors);
				}
			}

			foreach (KeyValuePair<Axial, HexagonDraw> pair in HexAxialGrid)
			{
				HexagonDraw hex = pair.Value;
				for (int j = 0; j < hex.Vertices.Length; j++)
				{
					Vector2 vert_a = hex.Vertices[j];
					Vector2 vert_b = (j + 1 < hex.Vertices.Length) ? hex.Vertices[j + 1] : hex.Vertices[0];
					DrawLine(vert_a, vert_b, hex.Colors[0], 2f, true);
				}
			}

			if (_axMousePosition != Axial.Empty)
			{
				Vector2 pxMouse = Axial.AxToPx(_offset, _sideLength, _axMousePosition);
				DrawCircle(pxMouse, 3.0f, Colors.GreenYellow);

				DrawLine(_mousePos_cur, pxMouse, Colors.SeaGreen, 1.0f, true);
				DrawString(GetThemeFont("font"), _mousePos_cur + new Vector2(5, -5), _axMousePosition.ToString(), HorizontalAlignment.Left, -1, 16, Colors.GreenYellow);

				foreach (int i in Enum.GetValues(typeof(Axial.Cardinal)))
				{
					Axial axNeighbor = Axial.Neighbor(_axMousePosition, (Axial.Cardinal)i);
					Vector2 pxNeighbor = Axial.AxToPx(_offset, _sideLength, axNeighbor);
					DrawCircle(pxNeighbor, 1.0f, Colors.GreenYellow);
				}
			}
		}


		private void ProcessInput()
		{
			OnMouseClick();
			DetectMouseMovement();
		}

		bool _leftMouseClicked = false;

		private void OnMouseClick()
		{
			if (_leftMouseClicked)
				_leftMouseClicked = Input.IsMouseButtonPressed(MouseButton.Left);

			if (Input.IsMouseButtonPressed(MouseButton.Left))
			{
				if (!_leftMouseClicked)
				{
					_leftMouseClicked = true;
					_mousePos_cur = GetViewport().GetMousePosition();
					GD.Print($"Registered mouse input. Mouse position: {_mousePos_cur}");

					float maxX = GetViewportRect().End.X;
					float maxY = GetViewportRect().End.Y;

					GD.Print($"maxX:{maxX}, maxY:{maxY}");

					if (_mousePos_cur.X < 0 || _mousePos_cur.X > maxX
						|| _mousePos_cur.Y < 0 || _mousePos_cur.Y > maxY)
					{
						GD.Print($"Input is out of bounds. Ignoring input");
						return;
					}

					Axial axMouse = Axial.PxToAx(_offset, _sideLength, _mousePos_cur);

					int foundAxialIndex = -1;

					for (int i = 0; i < _axPoints.Length; i++)
					{
						if (_axPoints[i] == axMouse)
						{
							foundAxialIndex = i;
							break;
						}
					}

					if (foundAxialIndex < 0)
					{
						GD.Print($"Is new axial ({axMouse})");
						for (int i = _pxPoints.Length - 1; i > 0; i--)
						{
							_pxPoints[i] = _pxPoints[i - 1];
							_axPoints[i] = _axPoints[i - 1];
							_hexDraws[i] = _hexDraws[i - 1];
						}

						_pxPoints[0] = _mousePos_cur;
						_axPoints[0] = axMouse;

						HexagonDraw hexagonDraw = new HexagonDraw(Axial.AxToPx(_offset, _sideLength, _axPoints[0]), _sideLength, Colors.Black);
						_hexDraws[0] = hexagonDraw;
					}
					else
					{
						GD.Print($"Is NOT new axial ({axMouse})");
						bool isBlack = _hexDraws[foundAxialIndex].Colors[0] == Colors.Black;
						if (isBlack)
							_hexDraws[foundAxialIndex].Colors[0] = Colors.White;
						else
						{
							_hexDraws[foundAxialIndex] = HexagonDraw.Zero;
							_axPoints[foundAxialIndex] = Axial.Empty;
						}
					}

					QueueRedraw();

					// Use the mouse position to render a point on the screen
				}
			}
		}


		private Vector2 _mousePos_last = Vector2.Zero;
		private Vector2 _mousePos_cur = Vector2.Zero;
		private int _movement_check = 0;
		private static int _MOVEMENT_CHECK_MOD = 10;
		private static float _MOVE_CHECK_THRESH = 1f;

		private bool _mouseMovement => CompareMouseMovements(_mousePos_cur, _mousePos_last, _MOVE_CHECK_THRESH);

		private void DetectMouseMovement()
		{
			_movement_check = (_movement_check > 100)
			? 0
			: _movement_check++;

			if (_mouseMovement)
			{
				UpdateMousePositions();

				OnMouseMovement(_mouseMovement);
			}
			else
			{
				if (_movement_check % _MOVEMENT_CHECK_MOD == 0)
				{
					UpdateMousePositions();
					OnMouseMovement(_mouseMovement);
				}
			}
		}

		private void UpdateMousePositions()
		{
			_mousePos_last = _mousePos_cur;
			_mousePos_cur = GetViewport().GetMousePosition();
		}

		private static bool CompareMouseMovements(Vector2 current, Vector2 last, float threshold)
		{

			return (current - last).LengthSquared() > threshold;
		}

		private void OnMouseMovement(bool mouseMovement)
		{
			if (mouseMovement)
			{
				_axMousePosition = Axial.PxToAx(_offset, _sideLength, _mousePos_cur);
				QueueRedraw();
			}
		}

		private bool OnSideLengthChanged(float old_length, float new_length)
		{
			if (_disableScript || _disableDraw || !Engine.IsEditorHint())
				return false;

			GD.Print($"Side length was set to {new_length} from {old_length}. Modifying HexagonDraws and queuing redraw");

			if (!_awaitingRedraw)
			{
				Threaded_RedrawHexagons(old_length, new_length);
				return true;
			}
			else
			{
				GD.PrintErr("Cannot change side length because a preivous redraw process has not finished.");
				return false;
			}
		}

		private bool _awaitingRedraw = false;
		private async void Threaded_RedrawHexagons(float old_length, float new_length)
		{
			_awaitingRedraw = true;

			int length_hexAll = _hexDraws.Length;
			int length_hexUsed = HexDrawsUsed.Length;

			int maxTasks = 4;
			int tasksLength = Math.Min(maxTasks, length_hexUsed);

			if(tasksLength <= 0)
			{
				_awaitingRedraw = false;
				return;
			}

			int iterationsPerTask = length_hexUsed / tasksLength;
			int remainderIterations = length_hexUsed % tasksLength;

			Task[] tasks = new Task[tasksLength];

			HexagonDraw[] newHexDraws = _hexDraws;
			Axial[] newAxPoints = _axPoints;

			Vector2 firstOrigin = HexDrawsUsed[0].origin;
			Axial firstAxial = Axial.PxToAx(_offset, old_length, HexDrawsUsed[0].origin);

			for (int i = 0; i < tasksLength; i++)
			{
				int taskIndex = i;
				tasks[i] = Task.Run(() =>
				{
					int iterations = (taskIndex < tasksLength - 1) ? iterationsPerTask : iterationsPerTask + remainderIterations;

					for (int j = 0; j < iterations; j++)
					{
						int index = taskIndex * iterationsPerTask + j;

						if (index >= length_hexAll)
						{
							GD.PrintErr($"Index {index} is out of range {length_hexAll}");
							break;
						}

						HexagonDraw oldIterateHex = HexDrawsUsed[index];
						Axial oldIterateAxial = Axial.PxToAx(_offset, old_length, oldIterateHex.origin);

						Axial AxialDistance_FirstToIterate = oldIterateAxial - firstAxial;
						// The pixel distance between the first indexed Hexagon and the iterate Hexagon, using the new length
						Vector2 newPixelDistance_FirstToIterate = Axial.AxToPx(Vector2.Zero, new_length, AxialDistance_FirstToIterate);
						Vector2 newOrigin = firstOrigin + newPixelDistance_FirstToIterate;
						Axial newAxial = Axial.PxToAx(_offset, new_length, newOrigin);
						Vector2 newOrigin_adjusted = Axial.AxToPx(_offset, new_length, newAxial);

						newHexDraws[index] = new HexagonDraw(newOrigin_adjusted, new_length, oldIterateHex.Colors);
						newAxPoints[index] = newAxial;
					}
				});
			}

			await Task.WhenAll(tasks);

			_awaitingRedraw = false;
			_hexDraws = newHexDraws;
			_axPoints = newAxPoints;
			QueueRedraw();
		}
	}
}
