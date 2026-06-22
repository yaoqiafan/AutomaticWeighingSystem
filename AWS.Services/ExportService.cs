using AWS.Core.Entities;
using AWS.Core.Interfaces;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;

namespace AWS.Services;

public class ExportService : IExportService
{
    public async Task ExportToExcelAsync(IEnumerable<WeighingArchiveRecord> records, string filePath)
    {
        await Task.Run(() =>
        {
            var workbook = new XSSFWorkbook();
            var sheet = workbook.CreateSheet("过磅记录");

            var headerStyle = workbook.CreateCellStyle();
            var headerFont = workbook.CreateFont();
            headerFont.IsBold = true;
            headerStyle.SetFont(headerFont);

            string[] headers = ["磅单编号", "车牌号", "客户名称", "货物名称",
                "第一次称重时间", "第一次重量(kg)", "第二次称重时间", "第二次重量(kg)",
                "毛重(kg)", "皮重(kg)", "净重(kg)", "操作员", "存档时间", "备注"];

            var headerRow = sheet.CreateRow(0);
            for (int i = 0; i < headers.Length; i++)
            {
                var cell = headerRow.CreateCell(i);
                cell.SetCellValue(headers[i]);
                cell.CellStyle = headerStyle;
                sheet.SetColumnWidth(i, 18 * 256);
            }

            int rowIdx = 1;
            foreach (var r in records)
            {
                var row = sheet.CreateRow(rowIdx++);
                row.CreateCell(0).SetCellValue(r.TicketNo);
                row.CreateCell(1).SetCellValue(r.VehiclePlate);
                row.CreateCell(2).SetCellValue(r.CustomerName);
                row.CreateCell(3).SetCellValue(r.GoodsName);
                row.CreateCell(4).SetCellValue(r.FirstWeighTime.ToString("yyyy-MM-dd HH:mm:ss"));
                row.CreateCell(5).SetCellValue(r.FirstWeight);
                row.CreateCell(6).SetCellValue(r.SecondWeighTime.ToString("yyyy-MM-dd HH:mm:ss"));
                row.CreateCell(7).SetCellValue(r.SecondWeight);
                row.CreateCell(8).SetCellValue(r.GrossWeight);
                row.CreateCell(9).SetCellValue(r.TareWeight);
                row.CreateCell(10).SetCellValue(r.NetWeight);
                row.CreateCell(11).SetCellValue(r.OperatorName);
                row.CreateCell(12).SetCellValue(r.ArchivedAt.ToString("yyyy-MM-dd HH:mm:ss"));
                row.CreateCell(13).SetCellValue(r.Remark ?? "");
            }

            using var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write);
            workbook.Write(fs);
        });
    }
}
