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

export interface FirestoreCustomer extends CustomerSummary {
  showPo: boolean;
  vendorId: string;
  statementOrInvoice: string;
  address1: string;
  address2: string;
  city: string;
  state: string;
  zip: string;
  emails: string[];
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
  missingInvoiceImages: InvoiceUploadMissingImage[];
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
  contactName?: string;
  contactPhone?: string;
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

export interface PagedSalesCalls {
  calls: SalesCall[];
  totalCount: number;
}

export interface AccountSummary {
  accountName: string;
  totalCalls: number;
  lastCallDate?: string;
  scheduledCalls: number;
  completedCalls: number;
  calls?: SalesCall[];
}

export interface TrendInvoicePoint {
  invoiceDate: string; invoiceNumber: string; amount: number; comparisonPeriod: string;
}
export interface TrendCustomerResult {
  customerNumber: number; customerName: string; priorYearInvoiceCount: number; currentInvoiceCount: number;
  priorYearAmount: number; currentAmount: number; amountChange: number; amountChangePercent: number;
  currentAggregate?: number; priorYearAggregate?: number; aggregateChange?: number; aggregateChangePercent?: number;
  isDeclining: boolean; invoices: TrendInvoicePoint[];
}
export interface TrendData {
  rangeKey: string; fromDate: string; toDate: string; priorYearFromDate: string; priorYearToDate: string;
  generatedAt: string; analysisVersion: string; aggregateBy: 'amount' | 'count';
  currentInvoiceCount: number; priorYearInvoiceCount: number;
  customerCount: number; decliningCustomerCount: number; currentTotalAmount: number; priorYearTotalAmount: number;
  currentTotalAggregate?: number; priorYearTotalAggregate?: number;
  customers: TrendCustomerResult[];
}
export interface TrendAnalysisResponse { data: TrendData; cached: boolean; }

export type Destination = 'invoice' | 'invoice-upload' | 'sales' | 'trends' | 'choose' | 'admin' | 'customer-admin' | 'password-change' | null;
export type Theme = 'light' | 'dark';