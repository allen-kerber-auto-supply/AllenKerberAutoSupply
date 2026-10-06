import { CommonModule } from '@angular/common';
import { Component, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { TrendCustomerResult, TrendData, TrendInvoicePoint } from '../shared/models';
import { TrendsService } from './trends.service';

interface TrendTooltip {
  customerName: string;
  point: TrendInvoicePoint;
  x: number;
  y: number;
}

@Component({
  selector: 'app-trends',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './trends.component.html',
  styleUrl: './trends.component.css'
})
export class TrendsComponent {
  private readonly service = inject(TrendsService);
  readonly today = this.formatDate(new Date());
  readonly chartWidth = 920;
  readonly chartHeight = 420;
  readonly chartLeft = 75;
  readonly chartRight = 890;
  readonly chartTop = 25;
  readonly chartBottom = 350;
  readonly chartPalette = [
    '#2563eb', '#7c3aed', '#db2777', '#ea580c', '#16a34a',
    '#0891b2', '#9333ea', '#ca8a04', '#dc2626', '#4f46e5',
    '#0f766e', '#c026d3', '#65a30d', '#0284c7', '#be123c'
  ];

  data: TrendData | null = null;
  hasSearched = false;
  loading = false;
  error = '';
  filter = '';
  selectedCustomerNumber: number | null = null;
  aggregateBy: 'amount' | 'count' = 'amount';
  hoveredPoint: TrendTooltip | null = null;
  private analysisRequestId = 0;
  dateFrom = this.formatDate(new Date(new Date().getFullYear(), new Date().getMonth(), 1));
  dateTo = this.today;

  get customers(): TrendCustomerResult[] {
    const search = this.filter.trim().toLowerCase();
    return (this.data?.customers || [])
      .filter(customer =>
        !search
        || (customer.customerName || '').toLowerCase().includes(search)
        || String(customer.customerNumber).includes(search))
      .sort((a, b) => this.compareCustomers(a, b));
  }

  get selectedCustomer(): TrendCustomerResult | null {
    return this.data?.customers.find(customer => customer.customerNumber === this.selectedCustomerNumber) || null;
  }

  get chartInvoices(): TrendInvoicePoint[] {
    return this.selectedCustomer ? this.invoicePoints(this.selectedCustomer) : [];
  }

  get chartPoints(): TrendInvoicePoint[] {
    if (this.aggregateBy === 'amount')
      return this.chartInvoices;

    const monthlyCounts = new Map<string, number>();
    for (const invoice of this.chartInvoices) {
      const month = invoice.invoiceDate.slice(0, 7);
      monthlyCounts.set(month, (monthlyCounts.get(month) || 0) + 1);
    }

    return Array.from(monthlyCounts.entries())
      .sort(([a], [b]) => a.localeCompare(b))
      .map(([month, count]) => ({
        invoiceDate: `${month}-01`,
        invoiceNumber: `${count} invoice${count === 1 ? '' : 's'}`,
        amount: count,
        comparisonPeriod: 'Monthly total'
      }));
  }

  get chartMinAmount(): number {
    return Math.min(0, ...this.chartPoints.map(point => point.amount));
  }

  get chartMaxAmount(): number {
    return Math.max(1, ...this.chartPoints.map(point => point.amount));
  }

  get chartYTicks(): number[] {
    if (this.aggregateBy === 'count') {
      const max = Math.max(1, Math.ceil(this.chartMaxAmount));
      const tickCount = Math.min(5, max + 1);
      return Array.from({ length: tickCount }, (_, index) =>
        Math.round(max - index * max / (tickCount - 1)));
    }

    const step = (this.chartMaxAmount - this.chartMinAmount) / 4;
    return [
      this.chartMaxAmount,
      this.chartMinAmount + step * 3,
      this.chartMinAmount + step * 2,
      this.chartMinAmount + step,
      this.chartMinAmount
    ];
  }

  formatChartAxisValue(value: number): string {
    if (this.aggregateBy === 'count') {
      const count = Math.round(value);
      return `${count} invoice${count === 1 ? '' : 's'}`;
    }
    return this.formatAggregateValue(value);
  }

  get chartStartDate(): string {
    const dates = this.chartPoints.map(point => point.invoiceDate);
    return dates.length
      ? dates.reduce((start, date) => date < start ? date : start)
      : this.data?.priorYearFromDate || this.dateFrom;
  }

  get chartEndDate(): string {
    const dates = this.chartPoints.map(point => point.invoiceDate);
    return dates.length
      ? dates.reduce((end, date) => date > end ? date : end)
      : this.data?.toDate || this.dateTo;
  }

  get chartDateTicks(): string[] {
    const start = this.toDateMs(this.chartStartDate);
    const end = this.toDateMs(this.chartEndDate);
    return [
      this.formatDate(new Date(start)),
      this.formatDate(new Date(start + (end - start) / 2)),
      this.formatDate(new Date(end))
    ];
  }

  get topDecliningAmountChange(): number {
    return this.selectedCustomer ? this.customerAggregateChange(this.selectedCustomer) : 0;
  }

  get topDecliningAveragePercent(): number {
    return this.selectedCustomer ? this.customerAggregateChangePercent(this.selectedCustomer) : 0;
  }

  analyze(): void {
    this.error = '';
    if (!this.dateFrom || !this.dateTo) {
      this.error = 'Select both a start date and an end date.';
      return;
    }
    if (this.dateFrom > this.dateTo) {
      this.error = 'The start date must be on or before the end date.';
      return;
    }

    this.loading = true;
    const requestId = ++this.analysisRequestId;
    const aggregateBy = this.aggregateBy;
    this.service.analyze(this.dateFrom, this.dateTo, aggregateBy).subscribe({
      next: response => {
        if (requestId !== this.analysisRequestId)
          return;
        this.data = response.data;
        this.hasSearched = true;
        this.aggregateBy = response.data.aggregateBy || this.aggregateBy;
        this.selectedCustomerNumber = null;
        this.hoveredPoint = null;
        this.loading = false;
      },
      error: error => {
        if (requestId !== this.analysisRequestId)
          return;
        this.error = error.error?.message || 'Unable to analyze trends right now.';
        this.loading = false;
      }
    });
  }

  onAggregateByChange(): void {
    this.analysisRequestId++;
    this.data = null;
    this.hasSearched = false;
    this.loading = false;
    this.error = '';
    this.filter = '';
    this.selectedCustomerNumber = null;
    this.hoveredPoint = null;
  }

  toggle(customerNumber: number): void {
    const isSelected = this.selectedCustomerNumber === customerNumber;
    this.selectedCustomerNumber = isSelected ? null : customerNumber;
    this.hoveredPoint = null;
  }

  closeSelectedCustomer(): void {
    this.selectedCustomerNumber = null;
    this.hoveredPoint = null;
  }

  trackCustomer(_index: number, customer: TrendCustomerResult): number {
    return customer.customerNumber;
  }

  customerLabel(customer: TrendCustomerResult): string {
    return customer.customerName || `Customer ${customer.customerNumber}`;
  }

  invoiceChartPoints(customer: TrendCustomerResult): string {
    return this.chartPointsFor(customer)
      .map(invoice => `${this.invoiceX(invoice)},${this.invoiceY(invoice.amount)}`)
      .join(' ');
  }

  invoiceX(invoice: TrendInvoicePoint): number {
    const start = this.toDateMs(this.chartStartDate);
    const end = this.toDateMs(this.chartEndDate);
    const range = end - start || 1;
    return this.chartLeft + ((this.toDateMs(invoice.invoiceDate) - start) / range) * (this.chartRight - this.chartLeft);
  }

  invoiceY(amount: number): number {
    const range = this.chartMaxAmount - this.chartMinAmount || 1;
    return this.chartBottom - ((amount - this.chartMinAmount) / range) * (this.chartBottom - this.chartTop);
  }

  chartColor(index: number): string {
    return this.chartPalette[index % this.chartPalette.length];
  }

  showTooltip(point: TrendInvoicePoint, customerName: string, event: MouseEvent): void {
    const container = (event.currentTarget as SVGElement).ownerSVGElement?.parentElement;
    const bounds = container?.getBoundingClientRect();
    const tooltipWidth = 220;
    const tooltipHeight = 78;
    const rawX = (bounds ? event.clientX - bounds.left : 0) + 14;
    const rawY = (bounds ? event.clientY - bounds.top : 0) + 14;
    const maxX = Math.max(8, (bounds?.width || tooltipWidth) - tooltipWidth - 8);
    const maxY = Math.max(8, (bounds?.height || tooltipHeight) - tooltipHeight - 8);
    this.hoveredPoint = {
      customerName,
      point,
      x: Math.min(Math.max(rawX, 8), maxX),
      y: Math.min(Math.max(rawY, 8), maxY)
    };
  }

  hideTooltip(): void {
    this.hoveredPoint = null;
  }

  exportGraph(): void {
    const selectedCustomer = this.selectedCustomer;
    const points = this.chartPoints;
    const rows = [
      ['Customer', 'Customer Number', this.aggregateBy === 'amount' ? 'Invoice Date' : 'Month',
        this.aggregateBy === 'amount' ? 'Invoice Number' : 'Metric', this.metricLabel, 'Comparison Period'],
      ...(selectedCustomer ? points.map(invoice => [
          this.customerLabel(selectedCustomer),
          String(selectedCustomer.customerNumber),
          invoice.invoiceDate,
          invoice.invoiceNumber,
          this.aggregateBy === 'count' ? String(invoice.amount) : invoice.amount.toFixed(2),
          invoice.comparisonPeriod
        ]) : [])
    ];
    const csv = rows.map(row => row.map(value => this.escapeCsv(value)).join(',')).join('\r\n');
    const blob = new Blob([csv], { type: 'text/csv;charset=utf-8;' });
    const url = URL.createObjectURL(blob);
    const link = document.createElement('a');
    link.href = url;
    link.download = `customer-invoices-${this.selectedCustomerNumber}-${this.dateFrom}-to-${this.dateTo}.csv`;
    link.click();
    URL.revokeObjectURL(url);
  }

  displayDate(value: string): string {
    const [year, month, day] = value.slice(0, 10).split('-').map(Number);
    return new Intl.DateTimeFormat('en-US', {
      month: 'short',
      day: 'numeric',
      year: 'numeric'
    }).format(new Date(year, month - 1, day));
  }

  displayShortDate(value: string): string {
    const [year, month, day] = value.slice(0, 10).split('-').map(Number);
    return new Intl.DateTimeFormat('en-US', {
      month: 'numeric',
      day: 'numeric',
      year: 'numeric'
    }).format(new Date(year, month - 1, day));
  }

  priorYearDate(value: string): string {
    const [year, month, day] = value.slice(0, 10).split('-').map(Number);
    return `${year - 1}-${String(month).padStart(2, '0')}-${String(day).padStart(2, '0')}`;
  }

  displayChartDate(value: string): string {
    if (this.aggregateBy === 'count') {
      const [year, month] = value.slice(0, 7).split('-').map(Number);
      return new Intl.DateTimeFormat('en-US', {
        month: 'short',
        year: 'numeric'
      }).format(new Date(year, month - 1, 1));
    }
    return this.displayDate(value);
  }

  get metricLabel(): string {
    return this.aggregateBy === 'count' ? 'Invoices' : 'Invoice amount';
  }

  formatAggregateValue(value: number): string {
    const safeValue = Number.isFinite(value) ? value : 0;
    return this.aggregateBy === 'count'
      ? `${safeValue} invoice${safeValue === 1 ? '' : 's'}`
      : new Intl.NumberFormat('en-US', {
        style: 'currency',
        currency: 'USD'
      }).format(safeValue);
  }

  currentTotalAggregate(): number {
    const value = this.data?.currentTotalAggregate;
    return Number.isFinite(value)
      ? value!
      : this.aggregateBy === 'count'
        ? this.data?.currentInvoiceCount || 0
        : this.data?.currentTotalAmount || 0;
  }

  priorYearTotalAggregate(): number {
    const value = this.data?.priorYearTotalAggregate;
    return Number.isFinite(value)
      ? value!
      : this.aggregateBy === 'count'
        ? this.data?.priorYearInvoiceCount || 0
        : this.data?.priorYearTotalAmount || 0;
  }

  customerCurrentAggregate(customer: TrendCustomerResult): number {
    return Number.isFinite(customer.currentAggregate)
      ? customer.currentAggregate!
      : this.aggregateBy === 'count' ? customer.currentInvoiceCount : customer.currentAmount;
  }

  customerPriorYearAggregate(customer: TrendCustomerResult): number {
    return Number.isFinite(customer.priorYearAggregate)
      ? customer.priorYearAggregate!
      : this.aggregateBy === 'count' ? customer.priorYearInvoiceCount : customer.priorYearAmount;
  }

  customerAggregateChange(customer: TrendCustomerResult): number {
    return Number.isFinite(customer.aggregateChange)
      ? customer.aggregateChange!
      : this.customerCurrentAggregate(customer) - this.customerPriorYearAggregate(customer);
  }

  customerAggregateChangePercent(customer: TrendCustomerResult): number {
    if (Number.isFinite(customer.aggregateChangePercent))
      return customer.aggregateChangePercent!;

    const prior = this.customerPriorYearAggregate(customer);
    const current = this.customerCurrentAggregate(customer);
    if (prior === 0)
      return current === 0 ? 0 : current > 0 ? 100 : -100;
    return (current - prior) / Math.abs(prior) * 100;
  }

  private compareCustomers(a: TrendCustomerResult, b: TrendCustomerResult): number {
    const declineDifference = Number(b.isDeclining) - Number(a.isDeclining);
    return declineDifference
      || this.customerAggregateChangePercent(a) - this.customerAggregateChangePercent(b)
      || this.customerAggregateChange(a) - this.customerAggregateChange(b)
      || a.customerName.localeCompare(b.customerName);
  }

  invoicePoints(customer: TrendCustomerResult): TrendInvoicePoint[] {
    return customer.invoices || [];
  }

  chartPointsFor(customer: TrendCustomerResult): TrendInvoicePoint[] {
    if (this.aggregateBy === 'amount')
      return this.invoicePoints(customer);

    const monthlyCounts = new Map<string, number>();
    for (const invoice of this.invoicePoints(customer)) {
      const month = invoice.invoiceDate.slice(0, 7);
      monthlyCounts.set(month, (monthlyCounts.get(month) || 0) + 1);
    }
    return Array.from(monthlyCounts.entries())
      .sort(([a], [b]) => a.localeCompare(b))
      .map(([month, count]) => ({
        invoiceDate: `${month}-01`,
        invoiceNumber: `${count} invoice${count === 1 ? '' : 's'}`,
        amount: count,
        comparisonPeriod: 'Monthly total'
      }));
  }

  private escapeCsv(value: string): string {
    const safeValue = /^[=+\-@]/.test(value) ? `'${value}` : value;
    return `"${safeValue.replaceAll('"', '""')}"`;
  }

  private toDateMs(value: string): number {
    const [year, month, day] = value.slice(0, 10).split('-').map(Number);
    return new Date(year, month - 1, day, 12).getTime();
  }

  private formatDate(date: Date): string {
    const year = date.getFullYear();
    const month = String(date.getMonth() + 1).padStart(2, '0');
    const day = String(date.getDate()).padStart(2, '0');
    return `${year}-${month}-${day}`;
  }
}
