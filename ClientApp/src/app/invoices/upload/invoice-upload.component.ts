import { CommonModule } from '@angular/common';
import { HttpClient, HttpEventType } from '@angular/common/http';
import { Component, EventEmitter, OnDestroy, OnInit, Output, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { forkJoin } from 'rxjs';
import { Invoice, InvoiceUploadMissingImage, InvoiceUploadReconciliation, MisreadBarcodeItem, UploadProgressState } from '../../shared/models';

@Component({
  selector: 'app-invoice-upload',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './invoice-upload.component.html',
  styleUrls: ['./invoice-upload.component.css']
})
export class InvoiceUploadComponent implements OnInit, OnDestroy {
  private readonly http = inject(HttpClient);
  @Output() backRequested = new EventEmitter<void>();
  @Output() invoiceViewRequested = new EventEmitter<Invoice>();

  selectedStore = 0;
  storeOptions: number[] = [];
  csvStatus = '';
  csvProgress = 0;
  imagesStatus = '';
  imagesProgress = 0;
  reconciliation: InvoiceUploadReconciliation = { missingInvoiceImages: [], missingInvoices: [] };
  missingImageDetails: InvoiceUploadMissingImage[] = [];
  missingInvoiceDrafts: Record<string, string> = {};
  misreadBarcodes: MisreadBarcodeItem[] = [];
  drafts: Record<string, { invoiceNumber: string; storeNumber: number }> = {};
  private excelTimer: number | null = null;
  private imagesTimer: number | null = null;

  ngOnInit() {
    this.loadStoreOptions();
    this.loadMisreadBarcodes();
  }

  ngOnDestroy() {
    this.stopTimer('excel');
    this.stopTimer('images');
  }

  loadStoreOptions() {
    this.http.get<number[]>('/api/invoices/stores').subscribe({
      next: stores => {
        this.storeOptions = (stores || []).sort((a, b) => a - b);
        if (!this.storeOptions.includes(this.selectedStore)) this.selectedStore = 0;
      },
      error: () => { this.storeOptions = []; this.selectedStore = 0; }
    });
  }

  loadReconciliation() {
    if (this.selectedStore <= 0) {
      this.reconciliation = { missingInvoiceImages: [], missingInvoices: [] };
      this.missingImageDetails = [];
      return;
    }
    this.http.get<InvoiceUploadReconciliation>(`/api/invoices/upload-reconciliation?storeNumber=${this.selectedStore}`).subscribe({
      next: result => {
        this.reconciliation = result || { missingInvoiceImages: [], missingInvoices: [] };
        this.missingInvoiceDrafts = Object.fromEntries(
          this.reconciliation.missingInvoices.map(invoiceNumber => [invoiceNumber, invoiceNumber]));
        const details = this.reconciliation.missingInvoiceImages.filter((item): item is InvoiceUploadMissingImage => typeof item !== 'string');
        const keys = this.reconciliation.missingInvoiceImageKeys || this.reconciliation.missingInvoiceImages.filter((item): item is string => typeof item === 'string');
        this.loadMissingDetails(details, keys);
      },
      error: () => { this.reconciliation = { missingInvoiceImages: [], missingInvoices: [] }; this.missingImageDetails = []; }
    });
  }

  uploadExcel(event: Event) {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    if (!file) return;
    if (!this.requireStore('csv')) { input.value = ''; return; }
    const formData = new FormData();
    formData.append('excelFile', file, file.name);
    formData.append('storeNumber', String(this.selectedStore));
    this.csvStatus = 'Uploading Excel file...'; this.csvProgress = 0;
    this.stopTimer('excel'); this.startTimer('excel');
    this.http.post<any>('/api/invoices/upload-excel', formData, { reportProgress: true, observe: 'events' }).subscribe({
      next: event => {
        if (event.type !== HttpEventType.Response || !event.body) return;
        const imported = event.body.imported ?? 0;
        const errors = event.body.errors?.length ? ` ${event.body.errors.join(' ')}` : '';
        this.stopTimer('excel'); this.csvProgress = 100;
        this.csvStatus = `Excel imported successfully (${imported} invoice${imported === 1 ? '' : 's'}).${errors}`;
        this.loadReconciliation(); this.loadMisreadBarcodes();
      },
      error: error => { this.stopTimer('excel'); this.csvStatus = error.error?.message || 'Unable to import the selected Excel file.'; this.csvProgress = 0; },
      complete: () => input.value = ''
    });
  }

  uploadImages(event: Event) {
    const input = event.target as HTMLInputElement;
    const files = Array.from(input.files || []);
    if (!files.length) return;
    if (!this.requireStore('images')) { input.value = ''; return; }
    const formData = new FormData();
    for (const file of files) formData.append('files', file, file.name);
    formData.append('storeNumber', String(this.selectedStore));
    this.imagesStatus = 'Uploading images...'; this.imagesProgress = 0;
    this.stopTimer('images'); this.startTimer('images');
    this.http.post<any>('/api/invoices/upload-images', formData, { reportProgress: true, observe: 'events' }).subscribe({
      next: event => {
        if (event.type !== HttpEventType.Response || !event.body) return;
        const processed = event.body.processed ?? 0;
        const errors = event.body.errors?.length ? ` ${event.body.errors.join(' ')}` : '';
        this.stopTimer('images'); this.imagesProgress = 100;
        this.imagesStatus = `Processed ${processed} image${processed === 1 ? '' : 's'}.${errors}`;
        this.loadReconciliation(); this.loadMisreadBarcodes();
      },
      error: error => { this.stopTimer('images'); this.imagesStatus = error.error?.message || 'Unable to upload the selected image folder.'; this.imagesProgress = 0; },
      complete: () => input.value = ''
    });
  }

  loadMisreadBarcodes() {
    this.http.get<MisreadBarcodeItem[]>('/api/invoices/misread-barcodes').subscribe({
      next: items => {
        this.misreadBarcodes = items || [];
        this.drafts = Object.fromEntries(this.misreadBarcodes.map(item => [item.id, { invoiceNumber: '', storeNumber: this.selectedStore }]));
      },
      error: () => { this.misreadBarcodes = []; this.drafts = {}; }
    });
  }

  openMisread(item: MisreadBarcodeItem) { window.open(`/api/invoices/misread-barcodes/${encodeURIComponent(item.id)}/view`, '_blank'); }

  resolveMisread(item: MisreadBarcodeItem) {
    const draft = this.drafts[item.id] || { invoiceNumber: '', storeNumber: 0 };
    if (!draft.invoiceNumber.trim()) { alert('Enter the invoice number before saving this image.'); return; }
    if (!draft.storeNumber || draft.storeNumber <= 0) { alert('Select the store number before saving this image.'); return; }
    this.http.post('/api/invoices/misread-barcodes/resolve', { id: item.id, invoiceNumber: draft.invoiceNumber.trim(), storeNumber: Number(draft.storeNumber) }).subscribe({
      next: () => { this.loadMisreadBarcodes(); this.loadReconciliation(); },
      error: error => alert(error.error?.message || 'Unable to resolve the selected misread barcode image.')
    });
  }

  viewMissingInvoice(invoiceNumber: string) {
    const normalized = (invoiceNumber || '').trim();
    if (!normalized || this.selectedStore <= 0) return;
    this.invoiceViewRequested.emit({
      invoiceNumber: normalized,
      storeNumber: this.selectedStore,
      customerNumber: 0,
      customerName: '',
      invoiceAmount: 0
    });
  }

  reassignMissingInvoice(invoiceNumber: string) {
    const replacement = (this.missingInvoiceDrafts[invoiceNumber] || '').trim();
    if (!replacement || this.selectedStore <= 0) return;
    if (replacement === invoiceNumber.trim()) return;

    this.http.post('/api/invoice-images/reassign', {
      storeNumber: this.selectedStore,
      currentInvoiceNumber: invoiceNumber,
      newInvoiceNumber: replacement
    }).subscribe({
      next: () => this.loadReconciliation(),
      error: error => alert(error.error?.message || 'Unable to update the invoice number for this image.')
    });
  }

  deleteMisread(item: MisreadBarcodeItem) {
    if (!confirm(`Delete the misread image "${item.fileName}"?`)) return;
    this.http.delete(`/api/invoices/misread-barcodes/${encodeURIComponent(item.id)}`).subscribe({
      next: () => this.loadMisreadBarcodes(),
      error: error => alert(error.error?.message || 'Unable to delete the selected misread barcode image.')
    });
  }

  private requireStore(type: 'csv' | 'images') {
    if (this.selectedStore > 0) return true;
    if (type === 'csv') { this.csvStatus = 'Please select a store number before importing Excel data.'; this.csvProgress = 0; }
    else { this.imagesStatus = 'Please select a store number before uploading invoice images.'; this.imagesProgress = 0; }
    return false;
  }

  private loadMissingDetails(details: InvoiceUploadMissingImage[], keys: string[]) {
    if (details.length || !keys.length) { this.missingImageDetails = details; return; }
    forkJoin(keys.map(key => this.http.get<Invoice[]>(`/api/invoices?invoiceNumber=${encodeURIComponent(key)}`))).subscribe({
      next: results => this.missingImageDetails = results.flat().map(invoice => ({ invoiceNumber: invoice.invoiceNumber, invoiceDate: invoice.invoiceDate, customerName: invoice.customerName, invoiceAmount: invoice.invoiceAmount })),
      error: () => this.missingImageDetails = []
    });
  }

  private startTimer(type: 'excel' | 'images') {
    const poll = () => this.http.get<UploadProgressState>(`/api/invoices/progress?operation=${type}`).subscribe({
      next: state => {
        const percent = Math.max(0, Math.min(100, Number(state?.percent) || 0));
        if (type === 'excel') { this.csvProgress = percent; this.csvStatus = state.message || 'Uploading Excel file...'; }
        else { this.imagesProgress = percent; this.imagesStatus = state.message || 'Uploading images...'; }
        if (state?.status === 'completed' || percent >= 100) this.stopTimer(type);
      },
      error: () => {}
    });
    poll();
    const timer = window.setInterval(poll, 500);
    if (type === 'excel') this.excelTimer = timer; else this.imagesTimer = timer;
  }

  private stopTimer(type: 'excel' | 'images') {
    const timer = type === 'excel' ? this.excelTimer : this.imagesTimer;
    if (timer !== null) window.clearInterval(timer);
    if (type === 'excel') this.excelTimer = null; else this.imagesTimer = null;
  }
}