using mccsx.Extensions;
using mccsx.Helpers;
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;

namespace mccsx.Statistics
{
    public static class ClusteringPlot
    {
        public static void PlotHeatmap<TRowKey, TColumnKey, TRowTag, TColumnTag>(
            string filepath,
            IDataFrame<TRowKey, TColumnKey, IVector<TColumnKey, TRowKey, TRowTag>, IVector<TRowKey, TColumnKey, TColumnTag>> vectors,
            int width,
            bool autoHeight,
            bool leftAlignedBadVectors,
            bool upToDownForVerticalText,
            (double scale, Color color)[] colorScheme,
            (string name, Color color)[]? colorBarScheme,
            Color lineColor,
            Color labelColor,
            ClusteringInfo<TRowKey, TColumnKey>? rowClustInfo = null,
            ClusteringInfo<TRowKey, TColumnKey>? columnClustInfo = null,
            Func<TRowTag, Color>? rowColorFn = null,
            Func<TColumnTag, Color>? columnColorFn = null)
            where TRowKey : notnull
            where TColumnKey : notnull
        {
            var colidx = columnClustInfo?.GetSortedIndex(leftAlignedBadVectors)?.ToList() ?? Enumerable.Range(0, vectors.ColumnCount).ToList();
            var rowidx = rowClustInfo?.GetSortedIndex(leftAlignedBadVectors)?.ToList() ?? Enumerable.Range(0, vectors.RowCount).ToList();
            int minTextHeight = Math.Max(12, width / 500);
            int maxTextHeight = Math.Min(75, width / 80);

            int expectedCellWidth = Math.Max(Math.Min(width * 3 / 4 / (colidx.Count + 1), maxTextHeight), minTextHeight);
            int height = autoHeight ? Math.Min(int.MaxValue / 4 / width, expectedCellWidth * (rowidx.Count + 1) + width * 3 / 4) : width;
            var canvas = Rectangle.FromLTRB(0, 0, width, height);
            int minSide = autoHeight ? canvas.Width : Math.Min(canvas.Width, canvas.Height);
            int marginTop = 0, marginBottom = marginTop;
            int marginLeft = 0, marginRight = marginLeft;
            bool horizonalSpectrum = false;

            //               margin-top
            // -----------------------------------------
            // |             | col-dendrogrem |ccluinfo|
            // |   legend    |----------------|--------|
            // |             |   col-colors   | label  |
            // |-------------|----------------|        |
            // |  row-  |row-|                |        |
            // |dengro- |col-|    heatmap     | row-   |
            // | gram   |ors |                |labels  |
            // |        |    |                |        |
            // |---------------------------------------|
            // |rcluinfo|label   col-labels   |unused  |
            // -----------------------------------------
            //             margin-bottom

            // legend: width/height = ~1/6 minSide
            // unused: width/height = 1/12 minSide
            // row-labels: width = unused.width
            // col-labels: height = unused.height
            // row-colors: width = 5% minSide
            // col-colors: height = 5% minSide
            // heatmap: width = remaining-width, height = remaining-height (rounded to integral cell dimensions)
            // cell-spacing: round(minSide / 2000)
            // cell: width = heat-map.width / vectors - cell-pacing

            var body = Rectangle.FromLTRB(marginLeft, marginTop, canvas.Width - marginRight, canvas.Height - marginBottom);
            var legend = new Rectangle(body.Left, body.Top, minSide / 6, minSide / 6);
            var unused = new Rectangle(body.Right - minSide / 12, body.Bottom - minSide / 12, minSide / 12, minSide / 12);
            var rlabel = new Rectangle(unused.Left, legend.Bottom, unused.Width, body.Height - unused.Height);
            var clabel = new Rectangle(body.Left, unused.Top, body.Width - unused.Width, unused.Height);
            var heatmap = new Rectangle(legend.Right, legend.Bottom, body.Width - legend.Width - unused.Width, body.Height - legend.Height - unused.Height);
            int cellSpacing = (int)Math.Round(Math.Max(1, minSide / 2000.0));
            int labelSpacing = (int)Math.Round(Math.Max(2, minSide / 1000.0));
            var cell = new Size((int)Math.Round((double)heatmap.Width / vectors.ColumnCount - cellSpacing), (int)Math.Round((double)heatmap.Height / vectors.RowCount - cellSpacing));
            var fullCell = new Size(cell.Width + cellSpacing, cell.Height + cellSpacing);
            int labelTextHeight = Math.Min(maxTextHeight, Math.Max(minTextHeight, cell.Height));
            int cellLabelDash = labelTextHeight;

            int diffx = (cell.Width + cellSpacing) * vectors.ColumnCount - heatmap.Width;
            int diffy = (cell.Height + cellSpacing) * vectors.RowCount - heatmap.Height;
            heatmap = Rectangle.FromLTRB(heatmap.X - diffx, heatmap.Y - diffy, heatmap.Right, heatmap.Bottom);
            legend.Width -= diffx;
            legend.Height -= diffy;
            var spectrum = new Rectangle(legend.Left + legend.Width / 20, legend.Top + legend.Height / 20, horizonalSpectrum ? legend.Width * 9 / 10 : legend.Width / 8, horizonalSpectrum ? legend.Height / 8 : legend.Height * 9 / 10);
            int legendLabelDash = horizonalSpectrum ? spectrum.Height / 3 : spectrum.Width / 3;
            int spectrumTextHeight = horizonalSpectrum ? spectrum.Height / 2 : spectrum.Width / 2;

            var rcolorbar = rowColorFn != null ? new Size(legend.Width / 6, cell.Height) : new Size(cellSpacing * 2, cell.Height);
            var ccolorbar = columnColorFn != null ? new Size(cell.Width, legend.Height / 6) : new Size(cell.Width, cellSpacing * 2);

            int dendroSegHeight = columnClustInfo != null ? (legend.Height - ccolorbar.Height) / (columnClustInfo.Depth + 1) : 0;
            int dendroSegWidth = rowClustInfo != null ? (legend.Width - rcolorbar.Width) / (rowClustInfo.Depth + 1) : 0;

            using var image = new Bitmap(canvas.Width, canvas.Height);

            using (var g = Graphics.FromImage(image))
            using (var labelBrush = new SolidBrush(labelColor))
            {
                g.Clear(Color.White);

                //*****************************/
                //      Draw the legend       */
                //*****************************/
                using (var legendLabelFont = new Font(FontFamily.GenericSansSerif, spectrumTextHeight, GraphicsUnit.Pixel))
                using (var legendBrush = new LinearGradientBrush(spectrum, Color.Black, Color.Black, horizonalSpectrum ? 0 : -90))
                using (var dashPen = new Pen(lineColor, cellSpacing))
                {
                    var cb = new ColorBlend
                    {
                        Positions = Enumerable.Range(0, colorScheme.Length).Select(i => (float)i / (colorScheme.Length - 1)).ToArray(),
                        Colors = colorScheme.Select(o => o.color).ToArray(),
                    };
                    legendBrush.InterpolationColors = cb;

                    // draw legend spectrum
                    g.FillRectangle(legendBrush, spectrum);

                    if (horizonalSpectrum)
                    {
                        var colorbarLegendPos = new Point(spectrum.Left, spectrum.Bottom + legendLabelDash + spectrumTextHeight + labelSpacing + legend.Width / 20);

                        foreach (float pos in cb.Positions)
                        {
                            // draw legend spectrum label dash
                            var anchor = new Point(spectrum.Left + (int)(pos * spectrum.Width), spectrum.Bottom);

                            g.DrawLine(dashPen, anchor.X, anchor.Y, anchor.X, anchor.Y + legendLabelDash);
                            anchor.Y += legendLabelDash + labelSpacing;

                            float textwidth = g.MeasureString((pos * 2 - 1).ToString("0.0"), legendLabelFont).Width;

                            // draw legend spectrum label
                            g.DrawString(
                                (pos * 2 - 1).ToString("0.0"),
                                legendLabelFont,
                                labelBrush,
                                Math.Max(0, anchor.X - textwidth / 2),
                                anchor.Y);
                        }

                        int barHeight = spectrum.Height * 4 / 3;

                        // draw row/column color horizontal legend
                        if (colorBarScheme != null)
                        {
                            for (int i = 0; i < colorBarScheme.Length; i++)
                            {
                                var (name, color) = colorBarScheme[i];
                                var anchor = new Point(colorbarLegendPos.X, colorbarLegendPos.Y + i * barHeight);
                                using (var brush = new SolidBrush(color))
                                    g.FillRectangle(brush, anchor.X, anchor.Y, spectrum.Height, spectrum.Height);
                                anchor.X += spectrum.Height + labelSpacing;
                                g.DrawString(name, legendLabelFont, labelBrush, anchor.X, anchor.Y + (spectrum.Height - legendLabelFont.Height) / 2);
                            }
                        }
                    }
                    else // vertical spectrum
                    {
                        float maxwidth = cb.Positions.Select(pos => g.MeasureString((pos * 2 - 1).ToString("0.0"), legendLabelFont).Width).Max();
                        var colorbarLegendPos = new Point(spectrum.Right + legendLabelDash + (int)maxwidth + labelSpacing + legend.Width / 20, spectrum.Top);

                        foreach (float pos in cb.Positions)
                        {
                            var anchor = new Point(spectrum.Right, (int)((1 - pos) * spectrum.Height) + spectrum.Top);

                            g.DrawLine(dashPen, anchor.X, anchor.Y, anchor.X + legendLabelDash, anchor.Y);
                            anchor.X += legendLabelDash + labelSpacing;

                            float textwidth = g.MeasureString((pos * 2 - 1).ToString("0.0"), legendLabelFont).Width;

                            // draw legend spectrum label
                            g.DrawString(
                                (pos * 2 - 1).ToString("0.0"),
                                legendLabelFont,
                                labelBrush,
                                anchor.X + maxwidth - textwidth,
                                Math.Max(0, anchor.Y - legendLabelFont.Height / 2));
                        }

                        int barHeight = spectrum.Width * 3 / 2;

                        // draw row/column color vertical legend
                        if (colorBarScheme != null)
                        {
                            for (int i = 0; i < colorBarScheme.Length; i++)
                            {
                                var (name, color) = colorBarScheme[i];
                                var anchor = new Point(colorbarLegendPos.X, colorbarLegendPos.Y + i * barHeight);
                                using (var brush = new SolidBrush(color))
                                    g.FillRectangle(brush, anchor.X, anchor.Y, spectrum.Width, spectrum.Width);
                                anchor.X += spectrum.Width + labelSpacing;
                                g.DrawString(name, legendLabelFont, labelBrush, anchor.X, anchor.Y + (spectrum.Width - legendLabelFont.Height) / 2);
                            }
                        }
                    }
                }

                using var rlabelFont = new Font(FontFamily.GenericSansSerif, labelTextHeight, GraphicsUnit.Pixel);
                using var dendroPen = new Pen(lineColor, cellSpacing);

                //*****************************/
                //      Draw the heatmap      */
                //*****************************/
                for (int r = 0; r < vectors.RowCount; r++)
                {
                    for (int c = 0; c < vectors.ColumnCount; c++)
                    {
                        // draw heatmap
                        double value = vectors.GetAt(r, c);
                        if (!double.IsNaN(value))
                        {
                            using var brush = new SolidBrush(ColorHelper.Map(colorScheme, value));
                            g.FillRectangle(brush, heatmap.Left + colidx[c] * fullCell.Width + cellSpacing, heatmap.Top + rowidx[r] * fullCell.Height + cellSpacing, cell.Width, cell.Height);
                        }
                    }
                }

                //*****************************/
                //     Draw the row labels    */
                //*****************************/
                for (int i = 0; i < vectors.RowCount; i++)
                {
                    var anchor = new Point(rlabel.Left, heatmap.Top + rowidx[i] * fullCell.Height + cell.Height / 2 + cellSpacing);

                    // draw row label dashes
                    g.DrawLine(dendroPen, anchor.X, anchor.Y, anchor.X + cellLabelDash, anchor.Y);
                    anchor.X += cellLabelDash + labelSpacing;

                    // draw row labels
                    g.DrawString(
                        vectors.RowKeys[i].ToString(),
                        rlabelFont,
                        labelBrush,
                        anchor.X,
                        anchor.Y - rlabelFont.Height / 2);

                    // draw row colors
                    using var brush = new SolidBrush(rowColorFn?.Invoke(vectors.GetRowAt(i).Tag) ?? lineColor);
                    g.FillRectangle(brush, heatmap.Left - rcolorbar.Width, anchor.Y - cell.Height / 2, rcolorbar.Width - cellSpacing, rcolorbar.Height);
                }

                // draw row colors label
                if (rowColorFn != null && vectors.RowTagName != null)
                {
                    var anchor = new Point(heatmap.Left - rcolorbar.Width + (rcolorbar.Width - cellSpacing) / 2, heatmap.Bottom);

                    // draw row colors label dashes
                    g.DrawLine(dendroPen, anchor.X, anchor.Y, anchor.X, anchor.Y + cellLabelDash);
                    anchor.Y += cellLabelDash + labelSpacing;

                    // draw row colors label
                    if (upToDownForVerticalText)
                    {
                        g.DrawStringRotated(
                            vectors.RowTagName,
                            90,
                            rlabelFont,
                            labelBrush,
                            anchor.X - rlabelFont.Height / 2 + rlabelFont.Height,
                            anchor.Y);
                    }
                    else
                    {
                        g.DrawStringRotated(
                            vectors.RowTagName,
                            -90,
                            rlabelFont,
                            labelBrush,
                            anchor.X - rlabelFont.Height / 2,
                            anchor.Y + g.MeasureString(vectors.RowTagName, rlabelFont).Width);
                    }
                }

                //*****************************/
                //   Draw the column labels   */
                //*****************************/
                for (int i = 0; i < vectors.ColumnCount; i++)
                {
                    var anchor = new Point(heatmap.Left + colidx[i] * fullCell.Width + cellSpacing + cell.Width / 2, clabel.Top);

                    // draw column label dashes
                    g.DrawLine(dendroPen, anchor.X, anchor.Y, anchor.X, anchor.Y + cellLabelDash);
                    anchor.Y += cellLabelDash + labelSpacing;

                    // draw column labels
                    string label = vectors.ColumnKeys[i].ToString()!;
                    var textdimen = g.MeasureString(label, rlabelFont);

                    if (upToDownForVerticalText)
                    {
                        g.DrawStringRotated(
                            label,
                            90,
                            rlabelFont,
                            labelBrush,
                            anchor.X - rlabelFont.Height / 2 - cellSpacing + rlabelFont.Height,
                            anchor.Y);
                    }
                    else
                    {
                        g.DrawStringRotated(
                            label,
                            -90,
                            rlabelFont,
                            labelBrush,
                            anchor.X - rlabelFont.Height / 2 - cellSpacing,
                            anchor.Y + textdimen.Width);
                    }

                    // in case there is no column clustering, draw column labels in the top, too
                    if (columnClustInfo == null)
                    {
                        // draw column label dashes
                        anchor = new Point(heatmap.Left + colidx[i] * fullCell.Width + cellSpacing + cell.Width / 2, heatmap.Top - ccolorbar.Height);

                        g.DrawLine(dendroPen, anchor.X, anchor.Y, anchor.X, anchor.Y - cellLabelDash);

                        anchor.Y -= cellLabelDash + labelSpacing + (int)textdimen.Width;

                        // draw column labels
                        if (upToDownForVerticalText)
                        {
                            g.DrawStringRotated(
                                label,
                                90,
                                rlabelFont,
                                labelBrush,
                                anchor.X - rlabelFont.Height / 2 - cellSpacing + rlabelFont.Height,
                                anchor.Y);
                        }
                        else
                        {
                            g.DrawStringRotated(
                                label,
                                -90,
                                rlabelFont,
                                labelBrush,
                                anchor.X - rlabelFont.Height / 2 - cellSpacing,
                                anchor.Y + textdimen.Width);
                        }
                    }

                    // draw column colors
                    using var brush = new SolidBrush(columnColorFn?.Invoke(vectors.GetColumnAt(i).Tag) ?? lineColor);
                    g.FillRectangle(brush, anchor.X - cell.Width / 2, heatmap.Top - ccolorbar.Height, ccolorbar.Width, ccolorbar.Height - cellSpacing);
                }

                // draw column colors label
                if (columnColorFn != null && vectors.ColumnTagName != null)
                {
                    var anchor = new Point(heatmap.Right - cellSpacing, heatmap.Top - ccolorbar.Height + (ccolorbar.Height - cellSpacing) / 2);

                    // draw column colors label dashes
                    g.DrawLine(dendroPen, anchor.X, anchor.Y, anchor.X + cellLabelDash, anchor.Y);
                    anchor.X += cellLabelDash + labelSpacing;

                    // draw column colors label
                    g.DrawString(
                        vectors.ColumnTagName,
                        rlabelFont,
                        labelBrush,
                        anchor.X,
                        anchor.Y - rlabelFont.Height / 2);
                }

                //*****************************/
                //   Draw the row dendrogram  */
                //*****************************/
                if (rowClustInfo != null)
                {
                    var rowNodePos = Enumerable.Range(0, vectors.RowCount)
                        .Select(i => new Point(heatmap.Left - rcolorbar.Width, heatmap.Top + i * fullCell.Height + cellSpacing + cell.Height / 2))
                        .ToList();

                    for (int i = 0; i < rowClustInfo.NodeCount; i++)
                    {
                        int idx1 = rowidx[rowClustInfo[i].ClusterIdx1], idx2 = rowidx[rowClustInfo[i].ClusterIdx2];
                        var connpos = new Point(heatmap.Left - rcolorbar.Width - rowClustInfo[i].Depth * dendroSegWidth, (rowNodePos[idx1].Y + rowNodePos[idx2].Y) / 2);

                        // draw row dendrogram
                        g.DrawLines(dendroPen, new[]
                        {
                            rowNodePos[idx1],
                            new Point(connpos.X, rowNodePos[idx1].Y),
                            new Point(connpos.X, rowNodePos[idx2].Y),
                            rowNodePos[idx2],
                        });

                        rowNodePos.Add(connpos);
                        rowidx.Add(rowidx.Count);
                    }

                    // draw row clustering info
                    g.DrawLines(dendroPen, new[]
                    {
                        new Point(rowNodePos.Last().X, heatmap.Bottom),
                        new Point(rowNodePos.Last().X, heatmap.Bottom + cellLabelDash / 2),
                        new Point(heatmap.Left - rcolorbar.Width, heatmap.Bottom + cellLabelDash / 2),
                        new Point(heatmap.Left - rcolorbar.Width, heatmap.Bottom),
                    });

                    var anchor = new Point((rowNodePos.Last().X + heatmap.Left - rcolorbar.Width) / 2, heatmap.Bottom + cellLabelDash / 2);
                    g.DrawLine(dendroPen, anchor.X, anchor.Y, anchor.X, anchor.Y + cellLabelDash / 2);
                    anchor.Y += cellLabelDash / 2 + labelSpacing;

                    var td1 = g.MeasureString(rowClustInfo.MetricName, rlabelFont);
                    var td2 = g.MeasureString(rowClustInfo.ClusterMethod, rlabelFont);
                    int offset = (int)(Math.Min(td1.Width, td2.Width) / 2);

                    if (upToDownForVerticalText)
                    {
                        g.DrawStringRotated(
                            rowClustInfo.MetricName,
                            90,
                            rlabelFont,
                            labelBrush,
                            anchor.X + labelTextHeight - labelTextHeight / 2 - cellSpacing + rlabelFont.Height,
                            anchor.Y);
                        g.DrawString("+", rlabelFont, labelBrush, anchor.X - labelTextHeight / 2 - cellSpacing, anchor.Y + offset);
                        g.DrawStringRotated(
                            rowClustInfo.ClusterMethod,
                            90,
                            rlabelFont,
                            labelBrush,
                            anchor.X - labelTextHeight - labelTextHeight / 2 - cellSpacing + rlabelFont.Height,
                            anchor.Y);
                    }
                    else
                    {
                        g.DrawStringRotated(
                            rowClustInfo.MetricName,
                            -90,
                            rlabelFont,
                            labelBrush,
                            anchor.X - td1.Height * 3 / 2 - cellSpacing,
                            anchor.Y + td1.Width);
                        g.DrawString("+", rlabelFont, labelBrush, anchor.X - labelTextHeight / 2 - cellSpacing, anchor.Y + offset);
                        g.DrawStringRotated(
                            rowClustInfo.ClusterMethod,
                            -90,
                            rlabelFont,
                            labelBrush,
                            anchor.X + td2.Height / 2 - cellSpacing,
                            anchor.Y + td2.Width);
                    }
                }

                //*****************************/
                // Draw the column dendrogram */
                //*****************************/
                if (columnClustInfo != null)
                {
                    var colNodePos = Enumerable.Range(0, vectors.ColumnCount)
                        .Select(i => new Point(heatmap.Left + i * fullCell.Width + cellSpacing + cell.Width / 2, heatmap.Top - ccolorbar.Height))
                        .ToList();

                    for (int i = 0; i < columnClustInfo.NodeCount; i++)
                    {
                        int idx1 = colidx[columnClustInfo[i].ClusterIdx1], idx2 = colidx[columnClustInfo[i].ClusterIdx2];
                        var connpos = new Point((colNodePos[idx1].X + colNodePos[idx2].X) / 2, heatmap.Top - ccolorbar.Height - columnClustInfo[i].Depth * dendroSegHeight);

                        // draw column dendrogram
                        g.DrawLines(dendroPen, new[]
                        {
                            colNodePos[idx1],
                            new Point(colNodePos[idx1].X, connpos.Y),
                            new Point(colNodePos[idx2].X, connpos.Y),
                            colNodePos[idx2],
                        });

                        colNodePos.Add(connpos);
                        colidx.Add(colidx.Count);
                    }

                    // draw column clustering info
                    g.DrawLines(dendroPen, new[]
                    {
                        new Point(heatmap.Right, colNodePos.Last().Y),
                        new Point(heatmap.Right + cellLabelDash / 2, colNodePos.Last().Y),
                        new Point(heatmap.Right + cellLabelDash / 2, heatmap.Top - ccolorbar.Height),
                        new Point(heatmap.Right, heatmap.Top - ccolorbar.Height),
                    });

                    var anchor = new Point(heatmap.Right + cellLabelDash / 2, (colNodePos.Last().Y + heatmap.Top - ccolorbar.Height) / 2);
                    g.DrawLine(dendroPen, anchor.X, anchor.Y, anchor.X + cellLabelDash / 2, anchor.Y);
                    anchor.X += cellLabelDash / 2 + labelSpacing;

                    int offset = (int)(Math.Min(g.MeasureString(columnClustInfo.MetricName, rlabelFont).Width, g.MeasureString(columnClustInfo.ClusterMethod, rlabelFont).Width) / 2);
                    g.DrawString(columnClustInfo.MetricName, rlabelFont, labelBrush, anchor.X, anchor.Y - labelTextHeight - labelTextHeight / 2);
                    g.DrawString("+", rlabelFont, labelBrush, anchor.X + offset, anchor.Y - labelTextHeight / 2);
                    g.DrawString(columnClustInfo.ClusterMethod, rlabelFont, labelBrush, anchor.X, anchor.Y + labelTextHeight - labelTextHeight / 2);
                }
            }
            image.Save(filepath);
        }
    }
}
