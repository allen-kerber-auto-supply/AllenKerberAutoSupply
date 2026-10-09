import { CommonModule } from '@angular/common';
import { HttpClient, HttpEventType } from '@angular/common/http';
import { Component, ElementRef, EventEmitter, OnDestroy, OnInit, Output, ViewChild, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Subscription } from 'rxjs';
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
  @ViewChild('misreadDialog') private misreadDialog!: ElementRef<HTMLDialogElement>;
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
  missingImageDateSortDirection: 'asc' | 'desc' = 'desc';
  missingInvoiceDrafts: Record<string, string> = {};
  processingMissingInvoices = new Map<string, 'saving' | 'deleting'>();
  misreadBarcodes: MisreadBarcodeItem[] = [];
  misreadBarcodesLoading = false;
  misreadBarcodesError = '';
  selectedMisreadBarcode: MisreadBarcodeItem | null = null;
  misreadImageUrl = '';
  misreadImageLoading = false;
  misreadImageError = '';
  misreadDraft = { invoiceNumber: '', storeNumber: 0, pageNumber: 1 };
  readonly misreadPageOptions = Array.from({ length: 10 }, (_, index) => index + 1);
  get misreadStoreOptions(): number[] {
    return [...new Set([...this.storeOptions, 302, 303, 304])].sort((a, b) => a - b);
  }
  misreadSaving = false;
  misreadSaveError = '';
  misreadSaveStatus = '';
  private misreadImageRequest: Subscription | null = null;
  private excelTimer: number | null = null;
  private imagesTimer: number | null = null;

  ngOnInit() {
    this.loadStoreOptions();
    this.loadMisreadBarcodes();
  }

  ngOnDestroy() {
    this.stopTimer('excel');
    this.stopTimer('images');
    this.clearMisreadImage();
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
        this.missingImageDetails = this.reconciliation.missingInvoiceImages;
        this.sortMissingImageDetailsByDate();
      },
      error: () => { this.reconciliation = { missingInvoiceImages: [], missingInvoices: [] }; this.missingImageDetails = []; }
    });
  }

  toggleMissingImageDateSort() {
    this.missingImageDateSortDirection = this.missingImageDateSortDirection === 'asc' ? 'desc' : 'asc';
    this.sortMissingImageDetailsByDate();
  }

  exportMissingImageDetails() {
    const escapeCsvCell = (value: string | number) => {
      const text = String(value);
      const safeText = typeof value === 'string' && /^[\t\r ]*[=+\-@]/.test(text) ? `'${text}` : text;
      return `"${safeText.replace(/"/g, '""')}"`;
    };
    const header = ['Invoice number', 'Date', 'Customer name', 'Amount'];
    const rows = this.missingImageDetails.map(invoice => {
      const timestamp = invoice.invoiceDate ? Date.parse(invoice.invoiceDate) : NaN;
      const date = Number.isFinite(timestamp) ? new Date(timestamp).toISOString().slice(0, 10) : '';
      return [
        escapeCsvCell(invoice.invoiceNumber),
        escapeCsvCell(date),
        escapeCsvCell(invoice.customerName),
        escapeCsvCell(invoice.invoiceAmount)
      ].join(',');
    });
    const csv = `\uFEFF${[header.map(escapeCsvCell).join(','), ...rows].join('\r\n')}`;
    const blob = new Blob([csv], { type: 'text/csv;charset=utf-8;' });
    const url = URL.createObjectURL(blob);
    const link = document.createElement('a');
    link.href = url;
    link.download = `invoices_missing_images_store_${this.selectedStore}_${new Date().toISOString().slice(0, 10)}.csv`;
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
    URL.revokeObjectURL(url);
  }

  private sortMissingImageDetailsByDate() {
    const direction = this.missingImageDateSortDirection === 'asc' ? 1 : -1;
    this.missingImageDetails = [...this.missingImageDetails].sort((left, right) => {
      const leftDate = left.invoiceDate ? Date.parse(left.invoiceDate) : NaN;
      const rightDate = right.invoiceDate ? Date.parse(right.invoiceDate) : NaN;
      const leftHasDate = Number.isFinite(leftDate);
      const rightHasDate = Number.isFinite(rightDate);
      if (!leftHasDate || !rightHasDate) return leftHasDate === rightHasDate ? 0 : leftHasDate ? -1 : 1;
      return (leftDate - rightDate) * direction;
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
    this.misreadBarcodesLoading = true;
    this.misreadBarcodesError = '';
    this.http.get<MisreadBarcodeItem[]>('/api/invoices/misread-barcodes').subscribe({
      next: items => {
        this.misreadBarcodes = items || [];
        this.misreadBarcodesLoading = false;
      },
      error: error => {
        this.misreadBarcodes = [];
        this.misreadBarcodesLoading = false;
        this.misreadBarcodesError = error.error?.message || 'Unable to load misread barcode images.';
      }
    });
  }

  showMisreadBarcodes() {
    const dialog = this.misreadDialog.nativeElement;
    if (!dialog.open) dialog.showModal();
    this.loadMisreadBarcodes();
  }

  closeMisreadBarcodes() {
    const dialog = this.misreadDialog.nativeElement;
    if (dialog.open) dialog.close();
    this.onMisreadDialogClosed();
  }

  onMisreadDialogClosed() {
    this.resetMisreadSelection();
    this.misreadSaveStatus = '';
  }

  selectMisread(item: MisreadBarcodeItem) {
    this.clearMisreadImage();
    this.selectedMisreadBarcode = item;
    const fileName = item.fileName.toUpperCase();
    const storeNumber = fileName.includes('TC') ? 304 : fileName.includes('LP') ? 302 : 303;
    this.misreadDraft = { invoiceNumber: '', storeNumber, pageNumber: 1 };
    this.misreadSaveError = '';
    this.loadMisreadImage(item);
  }

  loadMisreadImage(item: MisreadBarcodeItem) {
    this.misreadImageRequest?.unsubscribe();
    this.misreadImageLoading = true;
    this.misreadImageError = '';
    this.misreadImageRequest = this.http.get(`/api/invoices/misread-barcodes/${encodeURIComponent(item.id)}/view`, { responseType: 'blob' }).subscribe({
      next: image => {
        if (this.selectedMisreadBarcode?.id !== item.id) return;
        this.misreadImageUrl = URL.createObjectURL(image);
        this.misreadImageLoading = false;
      },
      error: error => {
        if (this.selectedMisreadBarcode?.id !== item.id) return;
        this.misreadImageLoading = false;
        this.misreadImageError = error.status === 404
          ? 'This misread barcode image could not be found.'
          : 'Unable to load this misread barcode image.';
      }
    });
  }

  zoomMisreadPreview(event: Event) {
    const image = event.currentTarget as HTMLImageElement;
    const preview = image.parentElement;
    if (!preview) return;
    preview.scrollLeft = preview.scrollWidth;
    preview.scrollTop = 0;
  }

  backToMisreadList() {
    this.resetMisreadSelection();
  }

  saveMisreadFromEnter(event: Event) {
    event.preventDefault();
    this.resolveMisread();
  }

  resolveMisread() {
    const item = this.selectedMisreadBarcode;
    const invoiceNumber = this.misreadDraft.invoiceNumber.trim();
    if (!item || this.misreadSaving) return;
    if (!invoiceNumber) { this.misreadSaveError = 'Enter the invoice number before saving this image.'; return; }
    if (!this.misreadDraft.storeNumber || this.misreadDraft.storeNumber <= 0) { this.misreadSaveError = 'Select the store number before saving this image.'; return; }

    this.misreadSaving = true;
    this.misreadSaveError = '';
    this.misreadSaveStatus = '';
    this.http.post('/api/invoices/misread-barcodes/resolve', {
      id: item.id,
      invoiceNumber,
      storeNumber: Number(this.misreadDraft.storeNumber),
      pageNumber: Number(this.misreadDraft.pageNumber)
    }).subscribe({
      next: () => {
        this.misreadSaving = false;
        this.misreadSaveStatus = `Saved ${item.fileName} to invoice ${invoiceNumber}.`;
        const currentIndex = this.misreadBarcodes.findIndex(candidate => candidate.id === item.id);
        const nextItem = currentIndex >= 0 ? this.misreadBarcodes[currentIndex + 1] : undefined;
        this.misreadBarcodes = this.misreadBarcodes.filter(candidate => candidate.id !== item.id);
        this.loadMisreadBarcodes();
        this.loadReconciliation();
        if (nextItem) this.selectMisread(nextItem);
        else this.closeMisreadBarcodes();
      },
      error: error => {
        this.misreadSaving = false;
        this.misreadSaveError = (typeof error.error === 'string' ? error.error : error.error?.message)
          || 'Unable to save the selected misread barcode image.';
      }
    });
  }

  resetMisreadSelection() {
    this.clearMisreadImage();
    this.selectedMisreadBarcode = null;
    this.misreadSaveError = '';
  }

  private clearMisreadImage() {
    this.misreadImageRequest?.unsubscribe();
    this.misreadImageRequest = null;
    if (this.misreadImageUrl) URL.revokeObjectURL(this.misreadImageUrl);
    this.misreadImageUrl = '';
    this.misreadImageLoading = false;
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
    if (!replacement || this.selectedStore <= 0 || this.isMissingInvoiceProcessing(invoiceNumber)) return;
    if (replacement === invoiceNumber.trim()) return;

    this.processingMissingInvoices.set(invoiceNumber, 'saving');
    this.http.post('/api/invoice-images/reassign', {
      storeNumber: this.selectedStore,
      currentInvoiceNumber: invoiceNumber,
      newInvoiceNumber: replacement
    }).subscribe({
      next: () => {
        this.processingMissingInvoices.delete(invoiceNumber);
        this.loadReconciliation();
      },
      error: error => {
        this.processingMissingInvoices.delete(invoiceNumber);
        alert(error.error?.message || 'Unable to update the invoice number for this image.');
      }
    });
  }

  deleteMissingInvoice(invoiceNumber: string) {
    if (this.selectedStore <= 0 || !confirm(`Delete invoice ${invoiceNumber} and all of its images?`)) return;
    this.http.delete(`/api/invoices/upload-reconciliation/${this.selectedStore}/invoice/${encodeURIComponent(invoiceNumber)}`).subscribe({
      next: () => this.loadReconciliation(),
      error: error => alert(error.error?.message || 'Unable to delete the selected invoice.')
    });
  }

  deleteMissingImage(invoiceNumber: string) {
    if (this.selectedStore <= 0 || this.isMissingInvoiceProcessing(invoiceNumber) || !confirm(`Delete all images for invoice ${invoiceNumber}?`)) return;
    this.processingMissingInvoices.set(invoiceNumber, 'deleting');
    this.http.delete(`/api/invoice-images/${this.selectedStore}/${encodeURIComponent(invoiceNumber)}`).subscribe({
      next: () => {
        this.processingMissingInvoices.delete(invoiceNumber);
        this.loadReconciliation();
      },
      error: error => {
        this.processingMissingInvoices.delete(invoiceNumber);
        alert(error.error?.message || 'Unable to delete the selected invoice images.');
      }
    });
  }

  isMissingInvoiceProcessing(invoiceNumber: string) {
    return this.processingMissingInvoices.has(invoiceNumber);
  }

  missingInvoiceOperation(invoiceNumber: string) {
    return this.processingMissingInvoices.get(invoiceNumber);
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