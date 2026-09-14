import { CommonModule } from '@angular/common';
import { Component, EventEmitter, Input, Output, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { InvoiceSearchPage, InvoiceService } from '../invoice.service';
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
  searchedCustomerName = '';
  invoices: Invoice[] = [];
  selectedInvoiceKeys = new Set<string>();
  error = '';
  showCustomerColumns = true;
  showCustomerNameColumn = true;
  invoiceSortKey: InvoiceSortKey = 'invoiceDate';
  invoiceSortDirection: 'asc' | 'desc' = 'desc';
  private searchPage = 0;
  hasMore = false;
  totalCount = 0;
  loading = false;

  search() {
    this.error = '';
    this.searchPerformed = true;
    const invoiceNumber = this.invoiceNumber.trim();
    const customerName = this.customerName.trim();
    this.showCustomerColumns = !!customerName || !!invoiceNumber;
    this.showCustomerNameColumn = !customerName;
    this.searchedCustomerName = '';

    if (invoiceNumber) {
      this.customerName = '';
      this.loadPage(0, false);
      return;
    }

    const customerNumber = customerName ? this.resolveCustomerNumber(customerName) : null;
    if (customerName && customerNumber == null) {
      this.error = 'Select a valid customer from the suggestions.';
      return;
    }
    this.searchedCustomerName = customerName;
    if (!this.dateFrom || !this.dateTo) {
      this.error = 'Select a date range to search by customer name.';
      return;
    }
    this.loadPage(0, false);
  }

  toggleSort(key: InvoiceSortKey) {
    if (this.invoiceSortKey === key) {
      this.invoiceSortDirection = this.invoiceSortDirection === 'asc' ? 'desc' : 'asc';
    } else {
      this.invoiceSortKey = key;
      this.invoiceSortDirection = key === 'invoiceDate' || key === 'customerName' ? 'desc' : 'asc';
    }
    this.search();
  }

  invoiceKey(invoice: Invoice) {
    return `${invoice.storeNumber || 0}|${invoice.invoiceNumber}`;
  }

  isSelected(invoice: Invoice) {
    return this.selectedInvoiceKeys.has(this.invoiceKey(invoice));
  }

  areAllInvoicesSelected() {
    return this.invoices.length > 0 && this.invoices.every(invoice => this.isSelected(invoice));
  }

  someInvoicesSelected() {
    return this.invoices.some(invoice => this.isSelected(invoice)) && !this.areAllInvoicesSelected();
  }

  toggleAllSelection(checked: boolean) {
    if (checked) {
      this.invoices.forEach(invoice => this.selectedInvoiceKeys.add(this.invoiceKey(invoice)));
    } else {
      this.invoices.forEach(invoice => this.selectedInvoiceKeys.delete(this.invoiceKey(invoice)));
    }
  }

  toggleSelection(invoice: Invoice) {
    const key = this.invoiceKey(invoice);
    if (this.selectedInvoiceKeys.has(key)) this.selectedInvoiceKeys.delete(key);
    else this.selectedInvoiceKeys.add(key);
  }

  selectedInvoices() {
    return this.invoices.filter(invoice => this.isSelected(invoice));
  }

  onResultsScroll(event: Event) {
    const element = event.target as HTMLElement;
    if (!this.loading && this.hasMore && element.scrollTop + element.clientHeight >= element.scrollHeight - 160) {
      this.loadPage(this.searchPage + 1, true);
    }
  }

  private loadPage(page: number, append: boolean) {
    const invoiceNumber = this.invoiceNumber.trim();
    const customerName = this.customerName.trim();
    const customerNumber = customerName ? this.resolveCustomerNumber(customerName) : undefined;
    const request = invoiceNumber
      ? this.invoiceService.searchByNumber(invoiceNumber, this.invoiceSortKey, this.invoiceSortDirection, page)
      : this.invoiceService.searchByDate(this.dateFrom, this.dateTo, customerNumber ?? undefined, this.invoiceSortKey, this.invoiceSortDirection, page);

    if (!append) {
      this.selectedInvoiceKeys.clear();
      this.invoices = [];
      this.totalCount = 0;
    }
    this.loading = true;
    request.subscribe({
      next: result => {
        const searchResult = result as InvoiceSearchPage;
        this.invoices = append ? [...this.invoices, ...(searchResult.items || [])] : (searchResult.items || []);
        this.searchPage = page;
        this.hasMore = searchResult.hasMore;
        this.totalCount = searchResult.totalCount;
        this.loading = false;
      },
      error: error => {
        this.loading = false;
        this.error = error.status === 403 ? 'Update your password before using invoice lookup.' : 'Unable to search invoices.';
      }
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