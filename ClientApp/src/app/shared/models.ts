export interface Invoice {
  invoiceNumber: string;
  storeNumber?: number;
  customerNumber: string | number;
  customerName: string;
  invoiceAmount: number;
  invoiceDate?: string;
  hasImages?: boolean;
}

export interface UserAccount {
  email: string;
  displayName: string;
  roles: string[];
  mustChangePassword: boolean;
}

export interface ViewerPage {
  pageIndex: number;
  url: string;
  blobUrl?: string;
  loaded: boolean;
  loading: boolean;
  error: boolean;
  errorMessage?: string;
}

export interface CustomerSummary {
  customerNumber: number;
  customerName: string;
}

export interface EmailGroup {
  customerNumber: number;
  customerName: string;
  invoices: Invoice[];
  availableEmails: string[];
  selectedEmails: string[];
  adHocEmail: string;
  loadingEmails: boolean;
}

export interface InvoiceEmailResult {
  customerNumber: number;
  email: string;
  success: boolean;
  error?: string;
}

export interface InvoiceUploadMissingImage {
  invoiceNumber: string;
  invoiceDate?: string;
  customerName: string;
  invoiceAmount: number;
}

export interface InvoiceUploadReconciliation {
  missingInvoiceImages: (InvoiceUploadMissingImage | string)[];
  missingInvoiceImageKeys?: string[];
  missingInvoices: string[];
}

export interface MisreadBarcodeItem {
  id: string;
  fileName: string;
  objectName: string;
  bucketName: string;
  contentType: string;
  createdUtc?: string;
}

export interface UploadProgressState {
  operation: string;
  busName: string;
  status: string;
  percent: number;
  message: string;
  processedCount: number;
  totalCount: number;
  updatedAtUtc?: string;
}

export interface SalesRep {
  id?: number | string;
  repName?: string;
  repEmail?: string;
  name?: string;
  email?: string;
  status?: string;
}

export interface SalesCustomer {
  customerNumber?: number;
  customerName?: string;
  accountName?: string;
  guid?: string;
  assignedSalesReps?: string[];
  repEmail?: string;
  repName?: string;
}

export interface SalesCall {
  id?: string;
  callID?: number;
  accountName: string;
  contactName?: string;
  phone?: string;
  contactPhone?: string;
  callDuration?: number;
  comments?: string;
  createdDate?: string;
  callDate?: string;
  followUpDate?: string;
  repName?: string;
  repEmail?: string;
  salesRepEmail?: string;
  status: number;
  isProspect?: boolean;
}

export interface AccountSummary {
  accountName: string;
  totalCalls: number;
  lastCallDate?: string;
  scheduledCalls: number;
  completedCalls: number;
  calls?: SalesCall[];
}

export type Destination = 'invoice' | 'invoice-upload' | 'sales' | 'choose' | 'admin' | 'password-change' | null;
export type Theme = 'light' | 'dark';