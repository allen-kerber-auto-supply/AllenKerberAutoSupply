import { CommonModule } from '@angular/common';
import { Component, EventEmitter, Input, OnDestroy, OnInit, Output, inject } from '@angular/core';
import { InvoiceService } from '../invoice.service';
import { InvoiceImageService } from '../invoice-image.service';
import { Invoice, Theme, ViewerPage } from '../../shared/models';

@Component({
  selector: 'app-invoice-viewer',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './invoice-viewer.component.html',
  styleUrls: ['./invoice-viewer.component.css']
})
export class InvoiceViewerComponent implements OnInit, OnDestroy {
  private readonly invoiceService = inject(InvoiceService);
  private readonly invoiceImageService = inject(InvoiceImageService);

  @Input() invoiceNumber = '';
  @Input() invoices: Invoice[] = [];
  @Input() storeNumber = 0;
  @Input() customer = '';
  @Input() customerNumber = '';
  @Input() theme: Theme = 'light';
  @Output() emailRequested = new EventEmitter<Invoice>();

  invoiceViews: Array<{ invoice: Invoice; pages: ViewerPage[]; loading: boolean; error: string }> = [];
  pages: ViewerPage[] = [];
  loading = false;
  error = '';

  ngOnInit() {
    if (!this.invoices.length && this.invoiceNumber) {
      this.invoices = [{ invoiceNumber: this.invoiceNumber, storeNumber: this.storeNumber, customerNumber: this.customerNumber, customerName: this.customer, invoiceAmount: 0 }];
    }
    if (this.invoices.length) {
      const first = this.invoices[0];
      this.invoiceNumber = first.invoiceNumber;
      this.storeNumber = first.storeNumber || this.storeNumber;
      this.customer = first.customerName || this.customer;
      this.customerNumber = first.customerNumber ? String(first.customerNumber) : this.customerNumber;
    }
    if (this.invoiceNumber) {
      document.title = this.invoices.length > 1 ? 'Invoice Print - Allen & Kerber Auto Supply' : `Invoice ${this.invoiceNumber} - Allen & Kerber Auto Supply`;
      this.load();
    }
  }

  ngOnDestroy() {
    for (const view of this.invoiceViews) {
      for (const page of view.pages) {
        if (page.blobUrl) URL.revokeObjectURL(page.blobUrl);
      }
    }
  }

  load() {
    this.loading = true;
    this.error = '';
    this.invoiceViews = this.invoices.map(invoice => ({ invoice, pages: [], loading: true, error: '' }));
    for (const view of this.invoiceViews) this.loadInvoice(view);
  }

  retryPage(view: { invoice: Invoice; pages: ViewerPage[]; loading: boolean; error: string }, page: ViewerPage) {
    page.url = this.invoiceImageService.pageUrl(view.invoice.invoiceNumber, view.invoice.storeNumber || 0, page.pageIndex, Date.now());
    this.loadPage(page);
  }

  requestEmail() {
    const emitInvoice = () => this.emailRequested.emit({
      invoiceNumber: this.invoiceNumber,
      storeNumber: this.storeNumber || 0,
      customerNumber: this.customerNumber || 0,
      customerName: this.customer || 'Customer',
      invoiceAmount: 0
    });

    if (this.customer && this.customerNumber) {
      emitInvoice();
      return;
    }

    this.invoiceService.getByNumber(this.invoiceNumber).subscribe({
      next: result => {
        this.applyInvoiceDetails(result.items);
        emitInvoice();
      },
      error: emitInvoice
    });
  }

  print() {
    window.print();
  }

  close() {
    window.close();
  }

  private applyInvoiceDetails(invoices: Invoice[]) {
    const match = this.storeNumber > 0
      ? invoices.find(invoice => invoice.storeNumber === this.storeNumber) || invoices[0]
      : invoices[0];
    if (!match) return;
    this.customer ||= match.customerName || '';
    this.customerNumber ||= match.customerNumber ? String(match.customerNumber) : '';
    this.storeNumber ||= match.storeNumber || 0;
  }

  private loadPage(page: ViewerPage) {
    page.loading = true;
    page.error = false;
    this.invoiceImageService.loadPage(page.url).subscribe({
      next: blob => {
        if (page.blobUrl) URL.revokeObjectURL(page.blobUrl);
        page.blobUrl = URL.createObjectURL(blob);
        page.loaded = true;
        page.loading = false;
      },
      error: error => {
        page.loading = false;
        page.error = true;
        page.errorMessage = error.status === 401
          ? 'Authentication required. Please sign in to view invoice images.'
          : error.status === 404
            ? `No image available for page ${page.pageIndex}.`
            : `Failed to load page ${page.pageIndex} (${error.status || 'network error'}).`;
      }
    });
  }

  private loadInvoice(view: { invoice: Invoice; pages: ViewerPage[]; loading: boolean; error: string }) {
    const invoice = view.invoice;
    this.invoiceImageService.lookup(invoice.invoiceNumber, invoice.storeNumber || 0).subscribe({
      next: lookup => {
        if (lookup.storeNumber) invoice.storeNumber = lookup.storeNumber;
        const pageIndices = lookup.pages?.length
          ? lookup.pages.map(page => page.pageIndex).sort((a, b) => a - b)
          : Array.from({ length: lookup.totalPages || 1 }, (_, index) => index + 1);
        view.pages = pageIndices.map(pageIndex => ({ pageIndex, url: this.invoiceImageService.pageUrl(invoice.invoiceNumber, invoice.storeNumber || 0, pageIndex), loaded: false, loading: false, error: false }));
        view.loading = false;
        for (const page of view.pages) this.loadPage(page);
        this.loading = this.invoiceViews.some(item => item.loading);
      },
      error: () => {
        view.loading = false;
        view.error = 'Unable to load invoice images.';
        this.loading = this.invoiceViews.some(item => item.loading);
      }
    });
  }
}