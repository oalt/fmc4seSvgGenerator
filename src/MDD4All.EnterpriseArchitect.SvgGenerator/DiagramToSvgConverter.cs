﻿/*
 * Copyright (c) MDD4All.de, Dr. Oliver Alt
 */
using System;
using System.Collections.Generic;
using System.Linq;
using MDD4All.SVG.DataModels;
using MDD4All.SVG.DataModels.Extensions;
#if EA_FACADE
using EAAPI = MDD4All.EAFacade.DataModels.Contracts;
using MDD4All.EAFacade.SvgGenerator.DataModels;
using MDD4All.EAFacade.Manipulations;
using MDD4All.EAFacade.SvgGenerator.Contracts;
#else
using EAAPI = EA;
using MDD4All.EnterpriseArchitect.SvgGenerator.DataModels;
using MDD4All.EnterpriseArchitect.Manipulations;
using MDD4All.EnterpriseArchitect.SvgGenerator.Contracts;
#endif
using System.Globalization;
using System.Drawing;

using NLog;
using System.Security.Cryptography;
using System.Text;
using MDD4All.SpecIF.DataModels.DiagramInterchange;



#if EA_FACADE
namespace MDD4All.EAFacade.SvgGenerator
#else
namespace MDD4All.EnterpriseArchitect.SvgGenerator
#endif
{
	public class DiagramToSvgConverter
	{
		private static Logger logger = LogManager.GetCurrentClassLogger();

		private int _maxX = 0;
		private int _maxY = 0;

		private EAAPI.Repository _repository;

		private IMetaDataCreator _metaDataCreator;

		public DiagramToSvgConverter(EAAPI.Repository repository, IMetaDataCreator metaDataCreator = null)
		{
			_repository = repository;
			_metaDataCreator = metaDataCreator;
		}

		public ScalableVectorGraphics ConvertDiagramToSVG(EAAPI.Diagram diagram)
		{
			_maxX = 0;

			NumberFormatInfo usFormat = CultureInfo.GetCultureInfo("en-US").NumberFormat;

			ScalableVectorGraphics result = new ScalableVectorGraphics();

			Group diagramCanvasGroup = new Group();

			diagramCanvasGroup.Metadata = null;

			diagramCanvasGroup.Title = new Title();

			diagramCanvasGroup.Title.Text = diagram.Name;

			diagramCanvasGroup.Class = "specif-diagram";

			ConvertDiagramElements(diagram, ref diagramCanvasGroup);

			ConvertDiagramLinks(diagram, ref diagramCanvasGroup);

			_maxX += 20;
			result.Width = _maxX.ToString();

			_maxY += 20;
			result.Height = _maxY.ToString();

			SVG.DataModels.Rectangle bounds = new SVG.DataModels.Rectangle()
			{
				X = "0",
				Y = "0",
				Width = _maxX.ToString(),
				Height = _maxY.ToString(),
				Fill = "transparent",
				Stroke = "black",
				StrokeWidth = "1"
			};

			result.Rectangles.Add(bounds);

			if (_metaDataCreator != null)
			{
				if(diagramCanvasGroup.Metadata == null)
                {
					diagramCanvasGroup.Metadata = new SpecIfMetadata();
                }

				diagramCanvasGroup.Metadata = _metaDataCreator.CreateMetaDataForDiagram(diagram, diagram.cy, _maxX);
			}

			result.Groups.Add(diagramCanvasGroup);

			return result;
		}

		private void ConvertDiagramLinks(EAAPI.Diagram diagram, ref Group result)
		{
			for (short counter = 0; counter < diagram.DiagramLinks.Count; counter++)
			{
				EAAPI.DiagramLink diagramLink = diagram.DiagramLinks.GetAt(counter) as EAAPI.DiagramLink;

				EAAPI.Connector connector = _repository.GetConnectorByID(diagramLink.ConnectorID);

				Group lineGroup = new Group();

				lineGroup.ID = diagramLink.InstanceID.ToString();

				lineGroup.Class = "specif-statement-diagram-element";

				lineGroup.Metadata = null;

				

				string connectorType = connector.Type;

				//if (connector.Stereotype == "access type")
				//if(connectorType != "Aggragation")
				{

					EAAPI.Element sourceElement = _repository.GetElementByID(connector.ClientID);

					EAAPI.DiagramObject sourceDiagramObject = GetDiagramObjectForElement(sourceElement.ElementID, diagram);

					EAAPI.Element targetElement = _repository.GetElementByID(connector.SupplierID);

					EAAPI.DiagramObject targetDiagramObject = GetDiagramObjectForElement(targetElement.ElementID, diagram);


					if (_metaDataCreator != null)
					{
                        if (lineGroup.Metadata == null)
                        {
                            lineGroup.Metadata = new Metadata();
                        }
                        lineGroup.Metadata = _metaDataCreator.CreateMetaDataForDiagramLink(diagramLink,
                                                                                           connector,
                                                                                           sourceDiagramObject,
                                                                                           targetDiagramObject,
                                                                                           sourceElement,
                                                                                           targetElement);
                    }

					bool startArrow = false;
					bool endArrow = false;

					if(connector.Direction == "Source -> Destination")
					{
						endArrow = true;
					}
					else if(connector.Direction == "Destination -> Source")
					{
						startArrow = true;
					}
					else if(connector.Direction == "Bi-Directional")
					{
						startArrow = true;
						endArrow = true;
					}

					int startX;
					int endX;
					int startY;
					int endY;



					//if(diagramLink.LineStyle == EAAPI.LinkLineStyle.LineStyleOrthogonalRounded)
					
					//EAAPI.LinkLineStyle.LineStyleOrthogonalRounded

					List<Point> linkPathPoints = ParseEaLinkPath(diagramLink.Path);
					List<Point> arcPoints = new List<Point>();
					List<bool> lineOrientations = new List<bool>();


                    // horizontal coordinates calculation
                    if (linkPathPoints.Count == 0) // direct line
                    {
                        // horizontal calculation
                        // source --> target
                        if (sourceDiagramObject.right < targetDiagramObject.left)
                        {
                            startX = sourceDiagramObject.right;
                            endX = targetDiagramObject.left;
                        }
                        else if (sourceDiagramObject.left > targetDiagramObject.right) // target <-- source
                        {
                            startX = sourceDiagramObject.left;
                            endX = targetDiagramObject.right;
                        }
                        else
                        {
                            startX = sourceDiagramObject.left + (sourceDiagramObject.right - sourceDiagramObject.left) / 2;
                            endX = startX;
                        }
                    }
                    else // bended line
                    {

                        // start of path

                        // horizontal calculation
                        // source --> target
                        if (sourceDiagramObject.right < linkPathPoints[0].X)
                        {
                            startX = sourceDiagramObject.right;
                        }
                        else if (sourceDiagramObject.left > linkPathPoints[0].X) // target <-- source
                        {
                            startX = sourceDiagramObject.left;
                        }
                        else
                        {
                            startX = linkPathPoints[0].X;
                        }

                        // end of path

                        // horizontal calculation
                        // target <-- last path
                        if (targetDiagramObject.right < linkPathPoints[linkPathPoints.Count - 1].X)
                        {
                            endX = targetDiagramObject.right;
                        }
                        else if (targetDiagramObject.left > linkPathPoints[linkPathPoints.Count - 1].X) // target --> last path
                        {
                            endX = targetDiagramObject.left;
                        }
                        else
                        {

                            endX = linkPathPoints[linkPathPoints.Count - 1].X;
                        }
                    }

                    // vertical coordinates calculation
                    if (linkPathPoints.Count == 0)
                    {
                        // vertical calculation
                        // source above target, vertical
                        if (-sourceDiagramObject.bottom < -targetDiagramObject.top)
                        {
                            startY = -sourceDiagramObject.bottom;
                            endY = -targetDiagramObject.top;
                        }
                        // source below target, vertival
                        else if (-sourceDiagramObject.top > -targetDiagramObject.bottom)
                        {
                            startY = -sourceDiagramObject.top;
                            endY = -targetDiagramObject.bottom;
                        }
                        else
                        {
                            startY = -sourceDiagramObject.top + (-sourceDiagramObject.bottom + sourceDiagramObject.top) / 2;
                            endY = startY;
                        }
                    }
                    else
                    {
                        // source above target, vertical
                        if (-sourceDiagramObject.bottom < linkPathPoints[0].Y)
                        {
                            startY = -sourceDiagramObject.bottom;

                        }
                        // source below target, vertival
                        else if (-sourceDiagramObject.top > linkPathPoints[0].Y)
                        {
                            startY = -sourceDiagramObject.top;

                        }
                        else
                        {
                            startY = linkPathPoints[0].Y; // -sourceDiagramObject.top + (-sourceDiagramObject.bottom + sourceDiagramObject.top) / 2;

                        }

                        // source above target, vertical
                        if (-targetDiagramObject.top > linkPathPoints[linkPathPoints.Count - 1].Y)
                        {
                            endY = -targetDiagramObject.top;

                        }
                        // source below target, vertival
                        else if (-targetDiagramObject.bottom < linkPathPoints[linkPathPoints.Count - 1].Y)
                        {
                            endY = -targetDiagramObject.bottom;

                        }
                        else
                        {
                            endY = linkPathPoints[linkPathPoints.Count - 1].Y; //-targetDiagramObject.top + (-targetDiagramObject.bottom + targetDiagramObject.top) / 2;

                        }
                    }


                    string path = "" + startX + "," + startY + " ";

					List<Point> segmentPoints = new List<Point>();

					CalculateConnectorSegmentCoordinates(startX, endX, startY, endY,
						diagramLink.LineStyle, linkPathPoints, ref segmentPoints, ref arcPoints, ref lineOrientations);

					ConnectorShape connectorShape = ConnectorShapeFactory.GetElementShape(connector, _repository);

					for(int segmentCounter = 0; segmentCounter < segmentPoints.Count; segmentCounter += 2)
                    {
                        Line line = new Line()
                        {
                            X1 = segmentPoints[segmentCounter].X.ToString(),
                            Y1 = segmentPoints[segmentCounter].Y.ToString(),
							X2 = segmentPoints[segmentCounter + 1].X.ToString(),
							Y2 = segmentPoints[segmentCounter + 1].Y.ToString(),
							Stroke = connectorShape.Color,
                            StrokeWidth = connectorShape.Width
                        };

						if(!string.IsNullOrEmpty(connectorShape.StrokeDashArray))
                        {
							line.StrokeDashArray = connectorShape.StrokeDashArray;
                        }

                        lineGroup.Lines.Add(line);

                        
					}

                    

                    #region ARCS

                    for (int arcCounter = 0; arcCounter < arcPoints.Count; arcCounter += 2)
					{
						Point startArc = arcPoints[arcCounter];
						Point endArc = arcPoints[arcCounter + 1];

						bool clockwise = true;

						bool horizontalLineSegmant = lineOrientations[arcCounter / 2];

						if(endArc.Y > startArc.Y && startArc.X > endArc.X) // down, left
						{
							if (horizontalLineSegmant)
							{
								clockwise = false;
							}
							else
							{
								clockwise = true;
							}
						}
						if (endArc.Y > startArc.Y && startArc.X < endArc.X) // down, right
						{
							if (horizontalLineSegmant)
							{
								clockwise = true;
							}
							else
							{
								clockwise = false;
							}
						}
						if (endArc.Y < startArc.Y && startArc.X > endArc.X) // up, left
						{
							if (horizontalLineSegmant)
							{
								clockwise = true;
							}
							else
							{
								clockwise = false;
							}
						}
						if (endArc.Y < startArc.Y && startArc.X < endArc.X) // up, right
						{
							if (horizontalLineSegmant)
							{
								clockwise = false;
							}
							else
							{
								clockwise = true;
							}
						}


						Path arcPath = new Path()
						{
							Stroke = connectorShape.Color,
							StrokeWidth = connectorShape.Width,
							Fill = "none"
						};

						if(!string.IsNullOrEmpty(connectorShape.StrokeDashArray))
                        {
							arcPath.StrokeDashArray = connectorShape.StrokeDashArray;
                        }

						string clockWiseFlag = clockwise ? "1" : "0";

						arcPath.Data = "M" + startArc.X + " " + startArc.Y + "A10 10 0 0 " + clockWiseFlag + " " + endArc.X + " " + endArc.Y;

						lineGroup.Paths.Add(arcPath);
					}

					#endregion

					#region ARROW_HEADS

					if (connectorType != "Aggregation")
					{
						// arrow heads
						if (linkPathPoints.Count == 0)
						{
							// single line
							// start arrow
							if (startArrow)
							{
								if (startX == endX)
								{
									// vertcal line
									Path startArrowPath;

									if (endY > startY)
									{
										startArrowPath = GetVerticalArrow(startX, startY, true);
									}
									else
									{
										startArrowPath = GetVerticalArrow(startX, startY, false);
									}

									lineGroup.Paths.Add(startArrowPath);
								}
								else
								{
									// horizontal line
									Path startArrowPath;

									if (endX > startX)
									{
										startArrowPath = GetHorizontalArrow(startX, startY, false);
									}
									else
									{
										startArrowPath = GetHorizontalArrow(startX, startY, true);
									}

									lineGroup.Paths.Add(startArrowPath);

								}
							}

							if (endArrow)
							{
								if (startX == endX)
								{
									Path endArrowPath;
									// vertical line
									if (endY > startY)
									{
										endArrowPath = GetVerticalArrow(endX, endY, false);
									}
									else
									{
										endArrowPath = GetVerticalArrow(endX, endY, true);
									}
									lineGroup.Paths.Add(endArrowPath);
								}
								else
								{
									Path endArrowPath;
									// horizontal line
									if (endX > startX)
									{
										endArrowPath = GetHorizontalArrow(endX, endY, true);
									}
									else
									{
										endArrowPath = GetHorizontalArrow(endX, endY, false);
									}
									lineGroup.Paths.Add(endArrowPath);

								}
							}

						}
						else
						{
							// bended line
							// start arrow
							if (startArrow)
							{
								if (startX == linkPathPoints[0].X)
								{
									// vertcal line
									Path startArrowPath;

									if (endY > linkPathPoints[0].Y)
									{
										startArrowPath = GetVerticalArrow(startX, startY, true);
									}
									else
									{
										startArrowPath = GetVerticalArrow(startX, startY, false);
									}

									lineGroup.Paths.Add(startArrowPath);
								}
								else
								{
									// horizontal line
									Path startArrowPath;

									if (endX > linkPathPoints[linkPathPoints.Count - 1].X)
									{
										startArrowPath = GetHorizontalArrow(startX, startY, false);
									}
									else
									{
										startArrowPath = GetHorizontalArrow(startX, startY, true);
									}

									lineGroup.Paths.Add(startArrowPath);

								}
							}

							if (endArrow)
							{
								if (linkPathPoints[linkPathPoints.Count - 1].X == endX)
								{
									Path endArrowPath;
									// vertical line
									if (endY > linkPathPoints[linkPathPoints.Count - 1].Y)
									{
										endArrowPath = GetVerticalArrow(endX, endY, false);
									}
									else
									{
										endArrowPath = GetVerticalArrow(endX, endY, true);
									}
									lineGroup.Paths.Add(endArrowPath);
								}
								else
								{
									Path endArrowPath;
									// horizontal line
									if (endX > linkPathPoints[linkPathPoints.Count - 1].X)
									{
										endArrowPath = GetHorizontalArrow(endX, endY, true);
									}
									else
									{
										endArrowPath = GetHorizontalArrow(endX, endY, false);
									}
									lineGroup.Paths.Add(endArrowPath);

								}
							}
						}

						#endregion

						result.Groups.Add(lineGroup);
					}
				}
			}
		}

		private void CalculateConnectorSegmentCoordinates(int startX,
														  int endX,
														  int startY,
														  int endY,
														  EAAPI.LinkLineStyle lineStyle,
														  List<Point> linkPathPoints,
														  ref List<Point> segmentPoints,
														  ref List<Point> arcPoints,
														  ref List<bool> lineOrientations)
		{
			if (linkPathPoints.Count == 0) // direct line
			{
				segmentPoints.Add(new Point
				{
					X = startX,
					Y = startY
				});

				segmentPoints.Add(new Point
				{
					X = endX,
					Y = endY
				});
			}
			else // bended line
            {
				

				int segmentCounter = 0;

				do
				{
					int x1, y1, x2, y2;

					if (segmentCounter == 0) // first segment
					{

						x1 = startX;
						y1 = startY;



						if (startX == linkPathPoints[0].X)
						{

							// vertical line segmant
							lineOrientations.Add(false);

							x2 = linkPathPoints[0].X;

							if (lineStyle == EAAPI.LinkLineStyle.LineStyleOrthogonalRounded)
							{
								if (linkPathPoints[0].Y < startY)
								{
									y2 = linkPathPoints[0].Y + 10;
								}
								else
								{
									y2 = linkPathPoints[0].Y - 10;
								}
							}
							else
                            {
								y2 = linkPathPoints[0].Y;
                            }
						}
						else
						{
							// horizontal line segment
							lineOrientations.Add(true);

							y2 = linkPathPoints[0].Y;

							if (lineStyle == EAAPI.LinkLineStyle.LineStyleOrthogonalRounded)
							{
								if (linkPathPoints[0].X < startX)
								{
									x2 = linkPathPoints[0].X + 10;
								}
								else
								{
									x2 = linkPathPoints[0].X - 10;
								}
							}
							else
                            {
								x2 = linkPathPoints[0].X;
                            }
						}

						if (lineStyle == EAAPI.LinkLineStyle.LineStyleOrthogonalRounded)
						{
							arcPoints.Add(new Point(x2, y2));
						}
					}
					else if (segmentCounter == linkPathPoints.Count) // last segment
					{

						x2 = endX;
						y2 = endY;

						if (endX == linkPathPoints[linkPathPoints.Count - 1].X)
						{
							// vertical line segmant
							lineOrientations.Add(false);

							x1 = linkPathPoints[linkPathPoints.Count - 1].X;
							if (lineStyle == EAAPI.LinkLineStyle.LineStyleOrthogonalRounded)
							{
								if (linkPathPoints[linkPathPoints.Count - 1].Y < endY)
								{
									y1 = linkPathPoints[linkPathPoints.Count - 1].Y + 10;
								}
								else
								{
									y1 = linkPathPoints[linkPathPoints.Count - 1].Y - 10;
								}
							}
							else
                            {
								y1 = linkPathPoints[linkPathPoints.Count - 1].Y;
                            }
						}
						else
						{
							// horizontal line segment
							lineOrientations.Add(true);
							y1 = linkPathPoints[linkPathPoints.Count - 1].Y;

							if (lineStyle == EAAPI.LinkLineStyle.LineStyleOrthogonalRounded)
							{
								if (linkPathPoints[linkPathPoints.Count - 1].X < endX)
								{
									x1 = linkPathPoints[linkPathPoints.Count - 1].X + 10;
								}
								else
								{
									x1 = linkPathPoints[linkPathPoints.Count - 1].X - 10;
								}
							}
							else
                            {
								x1 = linkPathPoints[linkPathPoints.Count - 1].X;
                            }
						}

						if (lineStyle == EAAPI.LinkLineStyle.LineStyleOrthogonalRounded)
						{
							arcPoints.Add(new Point(x1, y1));
						}
					}
					else // middle segment
					{

						Point bendPoint1 = linkPathPoints[segmentCounter - 1];
						Point bendPoint2 = linkPathPoints[segmentCounter];

						if (bendPoint1.X == bendPoint2.X)
						{
							// vertical line
							lineOrientations.Add(false);

							x1 = bendPoint1.X;
							x2 = bendPoint2.X;

							if (lineStyle == EAAPI.LinkLineStyle.LineStyleOrthogonalRounded)
							{
								if (bendPoint1.Y < bendPoint2.Y)
								{
									y1 = bendPoint1.Y + 10;
									y2 = bendPoint2.Y - 10;
								}
								else
								{
									y1 = bendPoint1.Y - 10;
									y2 = bendPoint2.Y + 10;
								}
							}
							else
                            {
								y1 = bendPoint1.Y;
								y2 = bendPoint2.Y;
                            }
						}
						else
						{
							// horizontal line
							lineOrientations.Add(true);

							y1 = bendPoint1.Y;
							y2 = bendPoint2.Y;

							if (lineStyle == EAAPI.LinkLineStyle.LineStyleOrthogonalRounded)
							{
								if (bendPoint1.X < bendPoint2.X)
								{
									x1 = bendPoint1.X + 10;
									x2 = bendPoint2.X - 10;
								}
								else
								{
									x1 = bendPoint1.X - 10;
									x2 = bendPoint2.X + 10;
								}
							}
							else
                            {
								x1 = bendPoint1.X;
								x2 = bendPoint2.X;
                            }

						}

						if (lineStyle == EAAPI.LinkLineStyle.LineStyleOrthogonalRounded)
						{
							arcPoints.Add(new Point(x1, y1));
							arcPoints.Add(new Point(x2, y2));
						}
					}

					segmentPoints.Add(new Point
					{
						X = x1,
						Y = y1
					});
					segmentPoints.Add(new Point
					{
						X = x2,
						Y = y2
					});

					if(x1 > _maxX)
                    {
						_maxX = x1;
                    }

					if(x2 > _maxX)
                    {
						_maxX = x2;
                    }

					if(y1 > _maxY)
                    {
						_maxY = y1;
                    }

					if (y2 > _maxY)
					{
						_maxY = y2;
					}

					segmentCounter++;

				} while (segmentCounter <= linkPathPoints.Count);


				
			}
		}

		private EAAPI.DiagramObject GetDiagramObjectForElement(int elementID, EAAPI.Diagram diagram)
		{
			EAAPI.DiagramObject result = null;

			for (short counter = 0; counter < diagram.DiagramObjects.Count; counter++)
			{
				EAAPI.DiagramObject diagramObject = diagram.DiagramObjects.GetAt(counter) as EAAPI.DiagramObject;

				if(diagramObject.ElementID == elementID)
				{
					result = diagramObject;
					break;
				}
			}

			return result;
		}

		private List<Point> ParseEaLinkPath(string path)
		{
			List<Point> result = new List<Point>();

			char[] elementSeparator = { ';' };

			char[] xySeparator = { ':' };

			string[] pathElements = path.Split(elementSeparator);

			foreach (string pointText in pathElements)
			{
				if (!string.IsNullOrWhiteSpace(pointText))
				{
					string[] pointSplitted = pointText.Split(xySeparator);

					Point p = new Point(int.Parse(pointSplitted[0]), -int.Parse(pointSplitted[1]));

					result.Add(p);
				}
			}

			return result;
		}

		private LinkGeometry ParseEaLinkGeometry(string geometry)
		{
			LinkGeometry result = new LinkGeometry();

			char[] elementSeparator = { ';' };

			char[] xySeparator = { ':' };

			char[] keyValueSepatartor = { '=' };

			string[] pathElements = geometry.Split(elementSeparator);

			foreach (string segment in pathElements)
			{
				if (!string.IsNullOrWhiteSpace(segment))
				{
					if(!segment.Contains(':'))
                    {
						string[] keyValuePair = segment.Split(keyValueSepatartor);

						if(keyValuePair.Length == 2)
                        {
							string key = keyValuePair[0];

							switch(key)
                            {
								case "SX":
									result.StartX = int.Parse(keyValuePair[1]);
									break;


								case "SY":
									result.StartY = int.Parse(keyValuePair[1]);
									break;

								case "EX":
									result.EndX = int.Parse(keyValuePair[1]);
									break;

								case "EY":
									result.EndY = int.Parse(keyValuePair[1]);
									break;





							}
						}
                    }
				}
			}

			return result;
		}


		private void ConvertDiagramElements(EAAPI.Diagram diagram, ref Group result)
		{
			NumberFormatInfo usFormat = CultureInfo.GetCultureInfo("en-US").NumberFormat;

			List<SvgElement> elements = new List<SvgElement>();

			List<SvgElement> portElements = new List<SvgElement>();

			for (short counter = 0; counter < diagram.DiagramObjects.Count; counter++)
			{
				EAAPI.DiagramObject diagramObject = diagram.DiagramObjects.GetAt(counter) as EAAPI.DiagramObject;

				EAAPI.Element element = _repository.GetElementByID(diagramObject.ElementID);

				Group elementGroup = new Group();

				elementGroup.ID = diagramObject.InstanceGUID.ToString();

				elementGroup.Class = "specif-resource-diagram-element";

				elementGroup.Metadata = null;

				if (_metaDataCreator != null)
				{
                    if (elementGroup.Metadata == null)
                    {
                        elementGroup.Metadata = new Metadata();
                    }
                    elementGroup.Metadata = _metaDataCreator.CreateMetaDataForDiagramObject(diagramObject, element);
                }

				string elementType = element.Type;
				string elementStereotype = element.Stereotype;

				ElementShape elementShape = ElementShapeFactory.GetElementShape(element, _repository);

				double middleX = (double)diagramObject.left + (((double)diagramObject.right - (double)diagramObject.left) / 2);
				double middleY = Math.Abs((double)(-diagramObject.top) + ((Math.Abs((double)-diagramObject.bottom) - Math.Abs((double)-diagramObject.top)) / 2));


				if (elementType == "Actor" && (elementStereotype == "human agent"))
				{
					int recatangleWidth = (diagramObject.right - diagramObject.left);
					int rectangleHeight = (-diagramObject.bottom) - (-diagramObject.top);

					int rectangleTop = -diagramObject.top;
					int rectangleLeft = diagramObject.left;

					elementGroup.Sequence = diagramObject.Sequence;

					SVG.DataModels.Rectangle rectangle = new SVG.DataModels.Rectangle()
					{
						X = diagramObject.left.ToString(),
						Y = "" + (diagramObject.top * -1),
						Width = recatangleWidth.ToString(usFormat),
						Height = rectangleHeight.ToString(usFormat),
						Fill = "white",
						Stroke = "black",
						StrokeWidth = "2"
					};

					if (diagramObject.left + recatangleWidth > _maxX)
					{
						_maxX = diagramObject.left + recatangleWidth;
					}

					if((diagramObject.top * -1) + rectangleHeight > _maxY)
                    {
						_maxY = (diagramObject.top * -1) + rectangleHeight;
					}

					elementGroup.Rectangles.Add(rectangle);

					

					float rowHeight = rectangleHeight / 6;
					float columWidth = recatangleWidth / 6;

					Circle circle = new Circle()
					{
						Cx = (rectangleLeft + (3 * columWidth)).ToString(usFormat),
						Cy = (rectangleTop + (2 * rowHeight)).ToString(usFormat),
						Radius = rowHeight.ToString(usFormat),
						Fill = "none",
						Stroke = "black",
						StrokeWidth = "1"
					};

					elementGroup.Circles.Add(circle);

					Line line1 = new Line()
					{
						X1 = (rectangleLeft + 3 * columWidth).ToString(usFormat),
						Y1 = (rectangleTop + 3 * rowHeight).ToString(usFormat),
						X2 = (rectangleLeft + 3 * columWidth).ToString(usFormat),
						Y2 = (rectangleTop + 4.5 * rowHeight).ToString(usFormat),
						Fill = "none",
						Stroke = "black",
						StrokeWidth = "1"
					};

					elementGroup.Lines.Add(line1);

					Line line2 = new Line()
					{
						X1 = (rectangleLeft + 3 * columWidth).ToString(usFormat),
						Y1 = (rectangleTop + 4.5 * rowHeight).ToString(usFormat),
						X2 = (rectangleLeft + 1 * columWidth).ToString(usFormat),
						Y2 = (rectangleTop + 6 * rowHeight).ToString(usFormat),
						Fill = "none",
						Stroke = "black",
						StrokeWidth = "1"
					};

					elementGroup.Lines.Add(line2);

					Line line3 = new Line()
					{
						X1 = (rectangleLeft + 3 * columWidth).ToString(usFormat),
						Y1 = (rectangleTop + 4.5 * rowHeight).ToString(usFormat),
						X2 = (rectangleLeft + 5 * columWidth).ToString(usFormat),
						Y2 = (rectangleTop + 6 * rowHeight).ToString(usFormat),
						Fill = "none",
						Stroke = "black",
						StrokeWidth = "1"
					};

					elementGroup.Lines.Add(line3);

					Line line4 = new Line()
					{
						X1 = (rectangleLeft + 1 * columWidth).ToString(usFormat),
						Y1 = (rectangleTop + 3.5 * rowHeight).ToString(usFormat),
						X2 = (rectangleLeft + 5 * columWidth).ToString(usFormat),
						Y2 = (rectangleTop + 3.5 * rowHeight).ToString(usFormat),
						Fill = "none",
						Stroke = "black",
						StrokeWidth = "1"
					};

					elementGroup.Lines.Add(line4);

					string classifierName = element.GetClassifierName(_repository);
					string name = element.Name;

					string nameTextToShow = classifierName;
					if (name != "")
					{
						nameTextToShow = name + ": " + classifierName;
					}

					Text nameText = new Text()
					{
						X = (diagramObject.left + ((diagramObject.right - diagramObject.left) / 2)).ToString(usFormat),
						Y = (-diagramObject.top + rectangleHeight + 12).ToString(usFormat),
						TextAnchor = "middle",
						Fill = "black",
						FontFamily = "Verdana",
						FontSize = "10",
						FontWeight = "bold",
						InlineSize = (diagramObject.right - diagramObject.left).ToString(usFormat),
						DisplayedText = nameTextToShow
					};


					float textWidth = GetTextDimension(nameTextToShow).Width;

					if (diagramObject.left + textWidth / 2 > _maxX)
					{
						_maxX = (int)(diagramObject.left + textWidth / 2);
					}

					if ((-diagramObject.top + rectangleHeight + 12) > _maxY)
					{
						_maxY = (-diagramObject.top + rectangleHeight + 12);
					}

					elementGroup.Texts.Add(nameText);

					elements.Add(elementGroup);
				}
				else
				{
					int elementWidth = (diagramObject.right - diagramObject.left);

					if (elementShape.MainShape == "Rectangle")
					{
						int recatangleWidth = (diagramObject.right - diagramObject.left);

						string strokeColor = elementShape.BorderColor;



						elementGroup.Sequence = diagramObject.Sequence;

						if (element.Stereotype == "agent")
						{
							string typeTag = element.GetTaggedValueString("Type");
							if (typeTag == "Chain")
							{
								strokeColor = "#FFA500";
							}
							else if (typeTag == "Software")
							{
								strokeColor = "#0000CD";
							}
							else if (typeTag == "Electronic")
							{
								strokeColor = "#228B22";
							}
							else if (typeTag == "Mechanical")
							{
								strokeColor = "#C0C0C0";
							}
						}

						int rectangleHeight = (-diagramObject.bottom) - (-diagramObject.top);

						SVG.DataModels.Rectangle rectangle = new SVG.DataModels.Rectangle()
						{
							X = diagramObject.left.ToString(),
							Y = "" + (diagramObject.top * -1),
							Width = recatangleWidth.ToString(),
							Height = rectangleHeight.ToString(),
							Fill = elementShape.FillColor,
							Stroke = strokeColor,
							StrokeWidth = elementShape.BorderWidth.ToString()


						};

						if (elementShape.CornerRadius > 0)
						{
							rectangle.HorizontalCornerRadius = elementShape.CornerRadius.ToString();
							rectangle.VerticalCornerRadius = elementShape.CornerRadius.ToString();
						}

						if(!string.IsNullOrEmpty(elementShape.StrokeDashArray))
                        {
							rectangle.StrokeDashArray = elementShape.StrokeDashArray;
                        }

						if (diagramObject.left + recatangleWidth > _maxX)
						{
							_maxX = diagramObject.left + recatangleWidth;
						}

						if ((diagramObject.top * -1) + rectangleHeight > _maxY)
						{
							_maxY = (diagramObject.top * -1) + rectangleHeight;
						}

						elementGroup.Rectangles.Add(rectangle);

						
						
					}
					else if (elementShape.MainShape == "Circle")
					{

						double radiusX = (((double)diagramObject.right - (double)diagramObject.left) / 2);
						double radiusY = ((Math.Abs((double)diagramObject.bottom) - Math.Abs((double)diagramObject.top)) / 2);


						elementGroup.Sequence = diagramObject.Sequence;
						//elementGroup.Metadata.EaType = element.Type;

						Circle circle = new Circle()
						{
							Cx = middleX.ToString(usFormat),
							Cy = middleY.ToString(usFormat),
							Radius = radiusX.ToString(usFormat),

							Fill = elementShape.FillColor,
							Stroke = elementShape.BorderColor,
							StrokeWidth = elementShape.BorderWidth.ToString()
						};

						if (!string.IsNullOrEmpty(elementShape.StrokeDashArray))
						{
							circle.StrokeDashArray = elementShape.StrokeDashArray;
						}

						

						elementGroup.Circles.Add(circle);

						elements.Add(elementGroup);
					}
					else if (elementShape.MainShape == "Ellipse")
					{

						double radiusX = (((double)diagramObject.right - (double)diagramObject.left) / 2);
						double radiusY = ((Math.Abs((double)diagramObject.bottom) - Math.Abs((double)diagramObject.top)) / 2);


						elementGroup.Sequence = diagramObject.Sequence;
						//elementGroup.Metadata.EaType = element.Type;

						Ellipse ellipse = new Ellipse()
						{
							Cx = middleX.ToString(usFormat),
							Cy = middleY.ToString(usFormat),
							RadiusX = radiusX.ToString(usFormat),
							RadiusY = radiusY.ToString(usFormat),

							Fill = elementShape.FillColor,
							Stroke = elementShape.BorderColor,
							StrokeWidth = elementShape.BorderWidth.ToString()
						};

						if (!string.IsNullOrEmpty(elementShape.StrokeDashArray))
						{
							ellipse.StrokeDashArray = elementShape.StrokeDashArray;
						}

						elementGroup.Ellipses.Add(ellipse);
					}

					

					if (elementType != "Port")
                    {
						List<string> textLines = CalculateTextLines(elementShape.MainLabel, elementWidth);

						int lineOffset = 0;

						foreach (string textLine in textLines)
						{

							Text nameText = new Text()
							{
								X = (diagramObject.left + ((diagramObject.right - diagramObject.left) / 2)).ToString(usFormat),
								Y = (-diagramObject.top + 24 + lineOffset).ToString(usFormat),
								TextAnchor = "middle",
								Fill = "black",
								FontFamily = "Verdana",
								FontSize = "10",
								FontWeight = elementShape.MainLabelFontWeight,
								InlineSize = (diagramObject.right - diagramObject.left).ToString(usFormat),
								DisplayedText = textLine
							};

							elementGroup.Texts.Add(nameText);
							lineOffset += 16;
						}

						elements.Add(elementGroup);
					}
					else // special label placement for ports
					{


						if (middleX + 8 > _maxX)
						{
							_maxX = (int)(middleX + 8);
						}

						if(middleY +8 > _maxY)
                        {
							_maxY = (int)(middleY + 8);
                        }

						// port label
						Point labelSize = diagramObject.GetLabelSize();
						Point labelOffset = diagramObject.GetLabelOffset();

						string classifierName = element.GetClassifierName(_repository);
						string name = element.Name;

						string nameTextToShow = classifierName;
						if (name != "")
						{
							nameTextToShow = name + ": " + classifierName;
						}

						logger.Debug(nameTextToShow + " sizeX = " + labelSize.X + ", sizeY = " + labelSize.Y + ", offsetX = " + labelOffset.X + ", offseetY = " + labelOffset.Y);


						List<string> textLines = CalculateTextLines(nameTextToShow, labelSize.X);

						int labelWidth = labelSize.X;

						int verticalTextLineDistance = 14;
						if (textLines.Count > 0)
						{
							verticalTextLineDistance = labelSize.Y / textLines.Count;
						}



						int n = 1;

						foreach (string textLine in textLines)
						{
							float labelX;
							float labelY;
							float offsetX;
							float offsetY;

							if (labelOffset.X == 0 && labelOffset.Y == 0)
							{
								labelX = (diagramObject.left - (labelWidth / 2));

								offsetX = 0;

								// k = textLines.Count
								labelY = (-diagramObject.top + ((n - (textLines.Count / 2)) * verticalTextLineDistance));
								offsetY = 0;
							}
							else
							{
								labelX = (diagramObject.left + ((diagramObject.right - diagramObject.left) / 2) + labelWidth / 2);
								offsetX = labelOffset.X - 10;

								labelY = (-diagramObject.top + ((-diagramObject.bottom + diagramObject.top) / 2)) + ((n - (textLines.Count / 2)) * verticalTextLineDistance);
								offsetY = labelOffset.Y;
							}


							if (labelX + labelWidth / 2 > _maxX)
							{
								_maxX = (int)(labelX + labelWidth / 2);
							}

							if(labelY + offsetY > _maxY)
                            {
								_maxY = (int)(labelY + offsetY);
                            }

							Text nameText = new Text()
							{
								X = (labelX + offsetX).ToString(usFormat),
								Y = (labelY + offsetY).ToString(usFormat),
								TextAnchor = "middle",
								Fill = "black",
								FontFamily = "Verdana",
								FontSize = "10",
								InlineSize = (diagramObject.right - diagramObject.left).ToString(usFormat),
								DisplayedText = textLine
							};

							elementGroup.Texts.Add(nameText);
							n++;
						}

						portElements.Add(elementGroup);

					}

					
				}
			}

			List<SvgElement> sortedElements = elements.OrderByDescending(el => el.Sequence).ToList();

			List<SvgElement> sortedPorts = portElements.OrderByDescending(el => el.Sequence).ToList();

			foreach (SvgElement sortedElement in sortedElements)
			{
				if(sortedElement.Description == null)
                {
					sortedElement.Description = new Description();
                }
				sortedElement.Description.Text = sortedElement.Sequence.ToString();

				//Console.WriteLine(sortedElement.Sequence);
				if (sortedElement is SVG.DataModels.Rectangle)
				{
					
					result.Rectangles.Add(sortedElement as SVG.DataModels.Rectangle);
				}
				else if (sortedElement is Circle)
				{
					result.Circles.Add(sortedElement as Circle);
				}
				else if (sortedElement is Group)
				{
					result.Groups.Add(sortedElement as Group);
				}
			}

			// add ports at the end to draw on top
			foreach (SvgElement sortedElement in sortedPorts)
			{
				if (sortedElement.Description == null)
				{
					sortedElement.Description = new Description();
				}
				sortedElement.Description.Text = sortedElement.Sequence.ToString();

				//Console.WriteLine(sortedElement.Sequence);
				if (sortedElement is SVG.DataModels.Rectangle)
				{

					result.Rectangles.Add(sortedElement as SVG.DataModels.Rectangle);
				}
				else if (sortedElement is Circle)
				{
					result.Circles.Add(sortedElement as Circle);
				}
				else if (sortedElement is Group)
				{
					result.Groups.Add(sortedElement as Group);
				}
			}
		}

		private List<string> CalculateTextLines(string text, int maxWidth)
		{
			List<string> result = new List<string>();

			char[] space = { ' ' };

			string[] textTokens = text.Split(space);

			List<string> tokens = new List<string>(textTokens);

			string textLine = "";

			foreach (string token in tokens)
			{
				string tmpText = "";
				if(textLine == "")
				{
					tmpText = token;
				}
				else
				{
					tmpText = textLine + " " + token;
				}

				float width = GetTextDimension(tmpText).Width;

				if (width / 1.3 > maxWidth)
				{
					logger.Debug("width = " + width + " maxWidth = " + maxWidth + " / " + tmpText);

					result.Add(textLine);
					textLine = token;
				}
				else
				{
					textLine = tmpText;
				}

			}
			if(textLine != "") // add last textline
			{
				result.Add(textLine);
			}

			return result;
		}

		private Path GetHorizontalArrow(int x, int y, bool leftToRight)
		{
			string pathData = "";

			int l = 5;

			if(leftToRight)
			{
				pathData = "M" + x + "," + y + " L" + (x - l) + "," + (y - l) + " L" + (x - l) + "," + (y + l) + " z";
			}
			else
			{
				pathData = "M" + x + "," + y + " L" + (x + l) + "," + (y - l) + " L" + (x + l) + "," + (y + l) + " z";
			}

			Path horizontalArrowPath = new Path()
			{
				Data = pathData,
				Fill = "black",
				Stroke = "black",
				StrokeWidth = "1",
				Description = new Description()
				{
					Text = "ha"
				}
			};

			return horizontalArrowPath;
		}

		private Path GetVerticalArrow(int x, int y, bool bottomToTop)
		{
			string pathData = "";

			int l = 5;

			if (bottomToTop)
			{
				pathData = "M" + x + "," + y + " L" + (x - l) + "," + (y + l) + " L" + (x + l) + "," + (y + l) + " z";
			}
			else
			{
				pathData = "M" + x + "," + y + " L" + (x + l) + "," + (y - l) + " L" + (x - l) + "," + (y - l) + " z";
			}

			Path verticalArrowPath = new Path()
			{
				Data = pathData,
				Fill = "black",
				Stroke = "black",
				StrokeWidth = "1",
				Description = new Description()
				{
					Text = "va"
				}
			};

			return verticalArrowPath;
		}

		private SizeF GetTextDimension(string text)
		{
			Font font = new Font("Verdana", 10, FontStyle.Regular);

			//create a bmp / graphic to use MeasureString on
			Bitmap b = new Bitmap(2200, 2200);
			Graphics g = Graphics.FromImage(b);

			//measure the string
			SizeF sizeOfString = new SizeF();
			sizeOfString = g.MeasureString(text, font);

			return sizeOfString;
		}
	
		private string GetSpecIfIdentifier(string eaGuid)
        {
			string result = eaGuid.Replace('-', '_');

			result = result.Replace("{", "");
			result = result.Replace("}", "");
			result = "_" + result;

			return result;
		}

		

	}
}
