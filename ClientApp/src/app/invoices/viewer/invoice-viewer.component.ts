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
  @Input() storeNumber = 0;
  @Input() customer = '';
  @Input() customerNumber = '';
  @Input() theme: Theme = 'light';
  @Output() emailRequested = new EventEmitter<Invoice>();

  pages: ViewerPage[] = [];
  loading = false;
  error = '';

  ngOnInit() {
    if (this.invoiceNumber) {
      document.title = `Invoice ${this.invoiceNumber} - Allen & Kerber Auto Supply`;
      this.load();
    }
  }

  ngOnDestroy() {
    for (const page of this.pages) {
      if (page.blobUrl) URL.revokeObjectURL(page.blobUrl);
    }
  }

  load() {
    this.loading = true;
    this.error = '';
    if (!this.customer || !this.customerNumber) {
      this.invoiceService.getByNumber(this.invoiceNumber).subscribe({
        next: invoices => this.applyInvoiceDetails(invoices),
        error: () => {}
      });
    }

    this.invoiceImageService.lookup(this.invoiceNumber, this.storeNumber).subscribe({
      next: lookup => {
        this.loading = false;
        if (lookup.storeNumber) this.storeNumber = lookup.storeNumber;
        const pageIndices = lookup.pages?.length
          ? lookup.pages.map(page => page.pageIndex).sort((a, b) => a - b)
          : Array.from({ length: lookup.totalPages || 1 }, (_, index) => index + 1);
        this.pages = pageIndices.map(pageIndex => ({
          pageIndex,
          url: this.invoiceImageService.pageUrl(this.invoiceNumber, this.storeNumber, pageIndex),
          loaded: false,
          loading: false,
          error: false
        }));
        for (const page of this.pages) this.loadPage(page);
      },
      error: () => {
        this.loading = false;
        const page: ViewerPage = {
          pageIndex: 1,
          url: this.invoiceImageService.pageUrl(this.invoiceNumber, this.storeNumber, 1),
          loaded: false,
          loading: false,
          error: false
        };
        this.pages = [page];
        this.loadPage(page);
      }
    });
  }

  retryPage(page: ViewerPage) {
    page.url = this.invoiceImageService.pageUrl(this.invoiceNumber, this.storeNumber, page.pageIndex, Date.now());
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
      next: invoices => {
        this.applyInvoiceDetails(invoices);
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
}