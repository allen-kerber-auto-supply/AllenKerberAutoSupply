import { CommonModule } from '@angular/common';
import { Component, EventEmitter, Input, Output, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { InvoiceService } from '../invoice.service';
import { CustomerSummary, Invoice } from '../../shared/models';

type InvoiceSortKey = 'invoiceDate' | 'invoiceAmount' | 'customerName' | 'customerNumber';

@Component({
  selector: 'app-invoice-search',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './invoice-search.component.html',
  styleUrls: ['./invoice-search.component.css']
})
export class InvoiceSearchComponent {
  private readonly invoiceService = inject(InvoiceService);

  @Input() customers: CustomerSummary[] = [];
  @Output() uploadRequested = new EventEmitter<void>();
  @Output() emailRequested = new EventEmitter<Invoice[]>();
  @Output() invoiceSelected = new EventEmitter<Invoice>();

  invoiceNumber = '';
  customerName = '';
  dateFrom = this.dateValue(-30);
  dateTo = this.dateValue(0);
  today = this.dateValue(0);
  searchPerformed = false;
  invoices: Invoice[] = [];
  selectedInvoiceKeys = new Set<string>();
  error = '';
  showCustomerColumns = true;
  invoiceSortKey: InvoiceSortKey = 'invoiceDate';
  invoiceSortDirection: 'asc' | 'desc' = 'desc';

  search() {
    this.error = '';
    this.searchPerformed = true;
    const invoiceNumber = this.invoiceNumber.trim();
    const customerName = this.customerName.trim();
    this.showCustomerColumns = !customerName || !!invoiceNumber;

    if (invoiceNumber) {
      this.customerName = '';
      this.runSearch(this.invoiceService.searchByNumber(invoiceNumber));
      return;
    }

    const customerNumber = customerName ? this.resolveCustomerNumber(customerName) : null;
    if (customerName && customerNumber == null) {
      this.error = 'Select a valid customer from the suggestions.';
      return;
    }
    if (!this.dateFrom || !this.dateTo) {
      this.error = 'Select a date range to search by customer name.';
      return;
    }
    this.runSearch(this.invoiceService.searchByDate(this.dateFrom, this.dateTo, customerNumber ?? undefined));
  }

  toggleSort(key: InvoiceSortKey) {
    if (this.invoiceSortKey === key) {
      this.invoiceSortDirection = this.invoiceSortDirection === 'asc' ? 'desc' : 'asc';
    } else {
      this.invoiceSortKey = key;
      this.invoiceSortDirection = key === 'invoiceDate' || key === 'customerName' ? 'desc' : 'asc';
    }
    this.sortInvoices();
  }

  invoiceKey(invoice: Invoice) {
    return `${invoice.storeNumber || 0}|${invoice.invoiceNumber}`;
  }

  isSelected(invoice: Invoice) {
    return this.selectedInvoiceKeys.has(this.invoiceKey(invoice));
  }

  toggleSelection(invoice: Invoice) {
    const key = this.invoiceKey(invoice);
    if (this.selectedInvoiceKeys.has(key)) this.selectedInvoiceKeys.delete(key);
    else this.selectedInvoiceKeys.add(key);
  }

  selectedInvoices() {
    return this.invoices.filter(invoice => this.isSelected(invoice));
  }

  private runSearch(request: ReturnType<InvoiceService['searchByNumber']>) {
    this.selectedInvoiceKeys.clear();
    request.subscribe({
      next: invoices => {
        this.invoices = invoices || [];
        this.sortInvoices();
      },
      error: error => this.error = error.status === 403 ? 'Update your password before using invoice lookup.' : 'Unable to search invoices.'
    });
  }

  private sortInvoices() {
    const direction = this.invoiceSortDirection === 'asc' ? 1 : -1;
    this.invoices = [...this.invoices].sort((a, b) => {
      if (this.invoiceSortKey === 'invoiceDate') {
        return ((a.invoiceDate ? new Date(a.invoiceDate).getTime() : 0) - (b.invoiceDate ? new Date(b.invoiceDate).getTime() : 0)) * direction;
      }
      if (this.invoiceSortKey === 'invoiceAmount') return ((Number(a.invoiceAmount) || 0) - (Number(b.invoiceAmount) || 0)) * direction;
      if (this.invoiceSortKey === 'customerName') return (a.customerName || '').localeCompare(b.customerName || '') * direction;
      return ((Number(a.customerNumber) || 0) - (Number(b.customerNumber) || 0)) * direction;
    });
  }

  private resolveCustomerNumber(typedName: string): number | null {
    const typed = typedName.trim().toLowerCase();
    const exact = this.customers.find(customer => customer.customerName.trim().toLowerCase() === typed);
    if (exact) return exact.customerNumber;
    const partial = this.customers.filter(customer => customer.customerName.toLowerCase().includes(typed));
    return partial.length === 1 ? partial[0].customerNumber : null;
  }

  private dateValue(offsetDays: number) {
    return new Date(Date.now() + offsetDays * 24 * 60 * 60 * 1000).toISOString().slice(0, 10);
  }
}