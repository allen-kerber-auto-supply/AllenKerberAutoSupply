import { Component, inject, OnInit, ViewEncapsulation } from '@angular/core';
import { CommonModule } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { forkJoin } from 'rxjs';
import { AuthService } from './auth/auth.service';
import { LoginComponent } from './auth/login/login.component';
import { PasswordChangeComponent } from './auth/password-change/password-change.component';
import { AccessDeniedComponent } from './auth/access-denied/access-denied.component';
import { WorkspaceChooserComponent } from './workspace/workspace-chooser.component';
import { ThemeService } from './shared/theme.service';
import { InvoiceViewerComponent } from './invoices/viewer/invoice-viewer.component';
import { InvoiceSearchComponent } from './invoices/search/invoice-search.component';
import { InvoiceUploadComponent } from './invoices/upload/invoice-upload.component';
import { InvoiceEmailModalComponent } from './invoices/email/invoice-email-modal.component';
import { SalesService } from './sales/sales.service';
import { SalesNavigationComponent } from './sales/sales-navigation.component';
import { ScheduledCallsComponent } from './sales/scheduled-calls.component';
import { CallDetailsComponent } from './sales/call-details.component';
import { SalesHistoryComponent } from './sales/sales-history.component';
import { SalesAdminComponent } from './sales/sales-admin.component';
import { CallEditModalsComponent } from './sales/call-edit-modals.component';
import { NewCallComponent } from './sales/new-call.component';
import { UserAdminComponent } from './admin/user-admin.component';
import { CustomerAdminComponent } from './admin/customer-admin.component';
import { AppHeaderComponent } from './shell/app-header.component';
import {
  AccountSummary,
  CustomerSummary,
  Destination,
  EmailGroup,
  Invoice,
  InvoiceEmailResult,
  SalesCall,
  SalesCustomer,
  SalesRep,
  Theme,
  UserAccount
} from './shared/models';

function toDateInputValue(date: Date): string { return date.toISOString().slice(0, 10); }

@Component({
  selector: 'app-root', standalone: true, imports: [CommonModule, LoginComponent, PasswordChangeComponent, AccessDeniedComponent, WorkspaceChooserComponent, InvoiceViewerComponent, InvoiceSearchComponent, InvoiceUploadComponent, InvoiceEmailModalComponent, SalesNavigationComponent, ScheduledCallsComponent, CallDetailsComponent, SalesHistoryComponent, SalesAdminComponent, CallEditModalsComponent, NewCallComponent, UserAdminComponent, CustomerAdminComponent, AppHeaderComponent],
  templateUrl: './app.component.html',
  styleUrl: './app.component.css',
  encapsulation: ViewEncapsulation.None,
})
export class AppComponent implements OnInit {
  private readonly http = inject(HttpClient); private readonly salesService = inject(SalesService); private readonly auth = inject(AuthService); private readonly themeService = inject(ThemeService);
  authenticated = false; denied = location.pathname === '/access-denied'; signingOut = false; name = ''; currentUserEmail = ''; roles: string[] = []; hasDualRoles = false; canManageUsers = false; canManageCustomers = false; mustChangePassword = false; destination: Destination = null; previousWorkspace: Destination = 'choose'; allCustomers: CustomerSummary[] = []; error = ''; emailModalOpen = false; emailGroups: EmailGroup[] = []; sendingEmails = false; emailResults: InvoiceEmailResult[] | null = null; emailError = ''; theme: Theme = this.initialTheme(); users: UserAccount[] = []; loadingUsers = false; usersError = ''; resettingUser: UserAccount | null = null; resetPasswordValue = ''; editingRolesUser: UserAccount | null = null; editingRoles: string[] = []; deletingUser: UserAccount | null = null; adminMessage = ''; adminError = false; roleOptions = ['InvoiceAdmin', 'InvoiceUser', 'CustomerInvoiceUser', 'SalesAdmin', 'SalesUser']; newUser = { displayName: '', email: '', temporaryPassword: this.generateTempPassword(), roles: [] as string[] };

  // Sales state
  salesTab: 'scheduled' | 'new-call' | 'history' | 'admin' = 'scheduled';
  salesReps: SalesRep[] = [];
  salesCustomers: SalesCustomer[] = [];
  unassignedSalesCustomers: SalesCustomer[] = [];
  filteredSalesCustomers: SalesCustomer[] = [];
  adminAccountFilter = '';
  adminRepFilter = '';
  selectedSalesFilterRep = '';
  scheduledCalls: SalesCall[] = [];
  scheduledAccountFilter = '';
  scheduledAccountOptions: string[] = [];
  loadingScheduledCalls = false;
  callHistory: SalesCall[] = [];
  filteredCallHistory: SalesCall[] = [];
  historyTotalCount = 0;
  callHistoryAccountOptions: string[] = [];
  selectedSummaryAccount = '';
  selectedAccountSummaryCalls: SalesCall[] = [];
  accountSummaries: AccountSummary[] = [];
  filteredAccountSummaries: AccountSummary[] = [];
  historyViewMode: 'records' | 'summary' = 'records';
  historyDateFrom = toDateInputValue(new Date(Date.now() - 7 * 24 * 60 * 60 * 1000));
  historyDateTo = toDateInputValue(new Date());
  historyAccountFilter = '';
  loadingHistory = false;
  loadingMoreHistory = false;
  private historyPage = 1;
  callSuccessMessage = '';
  private callSuccessToastTimer: number | null = null;

  get isSalesAdminView(): boolean {
    return this.isSalesAdmin;
  }
  completingCall: SalesCall | null = null;
  completingComments = '';
  completingFollowUpDate = '';
  selectedCallDetails: SalesCall | null = null;
  convertingProspect = false;
  editingCall: SalesCall | null = null;
  newSalesRep: SalesRep = { repName: '', repEmail: '' };
  newCustomerName = '';
  selectedAssignRepEmail = '';
  accountAssignmentMessage = '';

  get isSalesAdmin(): boolean { return this.roles.includes('SalesAdmin'); }
  get unassignedCustomerOptions(): CustomerSummary[] {
    const uniqueCustomers = new Map<string, CustomerSummary>();
    const assignedNames = new Set(
      this.salesCustomers
        .filter(salesCustomer => (salesCustomer.assignedSalesReps || []).length > 0)
        .map(salesCustomer => this.getAccountName(salesCustomer).trim().toLowerCase())
        .filter(Boolean)
    );

    const addCustomer = (customer: { customerNumber?: number; customerName?: string; accountName?: string } | null | undefined) => {
      if (!customer) return;
      const customerName = (customer.customerName || customer.accountName || '').trim();
      if (!customerName) return;
      const key = customerName.toLowerCase();
      if (assignedNames.has(key)) return;
      if (!uniqueCustomers.has(key)) {
        uniqueCustomers.set(key, {
          customerNumber: customer.customerNumber || 0,
          customerName
        });
      }
    };

    for (const customer of this.unassignedSalesCustomers) addCustomer(customer);
    if (!uniqueCustomers.size) {
      for (const customer of this.allCustomers) addCustomer(customer);
    }

    return Array.from(uniqueCustomers.values()).sort((a, b) => a.customerName.localeCompare(b.customerName));
  }
  
  // Viewer state
  isViewer = false;
  viewerInvoiceNumber = '';
  viewerInvoices: Invoice[] = [];
  viewerStoreNumber = 0;
  viewerCustomer = '';
  viewerCustNo = '';

  get initials() { return this.name.split(/\s+/).filter(Boolean).map(word => word[0]).join('').slice(0, 2).toUpperCase() || 'AK'; }

  constructor() {
    this.auth.getCurrentSession().subscribe(x => {
      this.authenticated = x.authenticated;
      this.name = x.name || '';
      this.currentUserEmail = (x.email || '').toLowerCase();
      this.roles = x.roles || [];
      this.mustChangePassword = x.mustChangePassword;
      if (x.authenticated && !this.isViewer) {
        this.setDestination();
        if (this.destination === 'invoice') {
          this.loadCustomers();
        }
        if (this.destination === 'sales') this.loadSalesData();
      }
    });
  }

  ngOnInit() {
    const params = new URLSearchParams(window.location.search);
    const pathname = window.location.pathname.toLowerCase();
    if (pathname.includes('invoice-view') || params.has('invoice')) {
      this.isViewer = true;
      this.viewerInvoiceNumber = (params.get('invoice') || '').trim();
      try {
        this.viewerInvoices = JSON.parse(params.get('invoices') || '[]') as Invoice[];
      } catch {
        this.viewerInvoices = [];
      }
      this.viewerStoreNumber = parseInt(params.get('store') || '0', 10);
      this.viewerCustomer = params.get('customer') || '';
      this.viewerCustNo = params.get('custNo') || '';
    }
  }

  initialTheme(): Theme { return this.themeService.getInitialTheme(); }
  toggleTheme() { this.theme = this.themeService.toggle(this.theme); }
  setDestination() { const sales = this.roles.includes('SalesAdmin') || this.roles.includes('SalesUser'); const invoice = this.roles.some(role => ['InvoiceAdmin', 'InvoiceUser', 'CustomerInvoiceUser'].includes(role)); this.hasDualRoles = sales && invoice; this.canManageUsers = this.roles.some(role => ['InvoiceAdmin', 'SalesAdmin'].includes(role)); this.canManageCustomers = this.roles.includes('InvoiceAdmin'); this.destination = this.mustChangePassword ? 'password-change' : this.hasDualRoles ? 'choose' : sales ? 'sales' : invoice ? 'invoice' : null; this.denied = this.destination === null; }
  go(destination: Destination) { if (this.destination !== 'admin' && this.destination !== 'customer-admin') this.previousWorkspace = this.destination; this.destination = destination; this.error = ''; if (destination === 'admin') this.loadUsers(); if (destination === 'invoice') this.loadCustomers(); if (destination === 'sales') this.loadSalesData(); }
  switchView() { this.go(this.destination === 'sales' ? 'invoice' : 'sales'); }
  loadCustomers() {
    if (this.allCustomers.length > 0) return;
    this.http.get<CustomerSummary[]>('/api/customers').subscribe({ next: customers => this.allCustomers = customers, error: () => {} });
  }
  loadUsers() { this.loadingUsers = true; this.usersError = ''; this.http.get<UserAccount[]>('/api/admin/users').subscribe({ next: users => { this.users = users; this.loadingUsers = false; }, error: e => { this.loadingUsers = false; this.usersError = e.error?.detail || e.error?.message || `Unable to load users (${e.status || 'network error'}).`; } }); }
  hasRole(role: string) { return this.newUser.roles.includes(role); }
  toggleRole(role: string, selected: boolean) { this.newUser.roles = selected ? [...this.newUser.roles, role] : this.newUser.roles.filter(value => value !== role); }
  generateTempPassword(): string {
    const charset = 'ABCDEFGHJKLMNPQRSTUVWXYZ23456789'; // excludes ambiguous chars (0/O, 1/I)
    const segment = () => Array.from({ length: 4 }, () => charset[Math.floor(Math.random() * charset.length)]).join('');
    return `${segment()}-${segment()}`;
  }
  createUser() { this.adminMessage = ''; const temporaryPassword = this.newUser.temporaryPassword; this.http.post<UserAccount>('/api/admin/users', this.newUser).subscribe({ next: user => { this.users = [...this.users, user].sort((a, b) => a.displayName.localeCompare(b.displayName)); this.newUser = { displayName: '', email: '', temporaryPassword: this.generateTempPassword(), roles: [] }; this.adminError = false; this.adminMessage = `User created. Temporary password: ${temporaryPassword} — share this securely.`; }, error: e => { this.adminError = true; this.adminMessage = e.error?.message || 'Unable to create user.'; } }); }
  startReset(user: UserAccount) { this.resettingUser = user; this.resetPasswordValue = this.generateTempPassword(); this.adminMessage = ''; }
  resetPassword() { if (!this.resettingUser) return; const temporaryPassword = this.resetPasswordValue; this.http.post<UserAccount>(`/api/admin/users/${encodeURIComponent(this.resettingUser.email)}/reset-password`, { temporaryPassword }).subscribe({ next: user => { this.users = this.users.map(item => item.email === user.email ? user : item); this.resettingUser = null; this.adminError = false; this.adminMessage = `Password reset. Temporary password: ${temporaryPassword} — share this securely.`; }, error: e => { this.adminError = true; this.adminMessage = e.error?.message || 'Unable to reset password.'; } }); }
  startEditRoles(user: UserAccount) { this.editingRolesUser = user; this.editingRoles = [...user.roles]; this.adminMessage = ''; }
  hasEditingRole(role: string) { return this.editingRoles.includes(role); }
  toggleEditingRole(role: string, selected: boolean) { this.editingRoles = selected ? [...this.editingRoles, role] : this.editingRoles.filter(value => value !== role); }
  saveRoles() { if (!this.editingRolesUser) return; if (!this.editingRoles.length) { this.adminError = true; this.adminMessage = 'Select at least one role.'; return; } this.http.put<UserAccount>(`/api/admin/users/${encodeURIComponent(this.editingRolesUser.email)}/roles`, { roles: this.editingRoles }).subscribe({ next: user => { this.users = this.users.map(item => item.email === user.email ? user : item); this.editingRolesUser = null; this.adminError = false; this.adminMessage = `Updated roles for ${user.displayName}.`; }, error: e => { this.adminError = true; this.adminMessage = e.error?.message || 'Unable to update roles.'; } }); }
  isSelf(user: UserAccount) { return !!this.currentUserEmail && user.email.toLowerCase() === this.currentUserEmail; }
  startDelete(user: UserAccount) { if (this.isSelf(user)) return; this.deletingUser = user; this.adminMessage = ''; }
  deleteUser() { if (!this.deletingUser) return; const user = this.deletingUser; this.http.delete(`/api/admin/users/${encodeURIComponent(user.email)}`).subscribe({ next: () => { this.users = this.users.filter(item => item.email !== user.email); this.deletingUser = null; this.adminError = false; this.adminMessage = `Deleted ${user.displayName}.`; }, error: e => { this.adminError = true; this.adminMessage = e.error?.message || 'Unable to delete user.'; } }); }
  logout() { if (this.signingOut) return; this.signingOut = true; this.auth.logout().subscribe({ next: () => location.reload(), error: () => { this.signingOut = false; this.error = 'Unable to sign out. Please try again.'; } }); }

  viewInvoice(invoice: Invoice) {
    const storeNumber = invoice.storeNumber || 0;
    const invoiceNumber = (invoice.invoiceNumber || '').trim();
    if (!invoiceNumber) return;

    const params = new URLSearchParams();
    params.set('invoice', invoiceNumber);
    if (storeNumber > 0) params.set('store', String(storeNumber));
    if (invoice.customerName) params.set('customer', invoice.customerName);
    if (invoice.customerNumber) params.set('custNo', String(invoice.customerNumber));

    window.open(`/invoice-view?${params.toString()}`, '_blank');
  }

  printInvoices(invoices: Invoice[]) {
    if (!invoices.length) return;
    const params = new URLSearchParams();
    params.set('invoice', (invoices[0].invoiceNumber || '').trim());
    params.set('invoices', JSON.stringify(invoices.map(invoice => ({
      invoiceNumber: invoice.invoiceNumber,
      storeNumber: invoice.storeNumber || 0,
      customerNumber: invoice.customerNumber || 0,
      customerName: invoice.customerName || ''
    }))));
    window.open(`/invoice-view?${params.toString()}`, '_blank');
  }

  openEmailModalForInvoices(selected: Invoice[]) {
    const byCustomer = new Map<number, EmailGroup>();
    for (const invoice of selected) {
      const customerNumber = Number(invoice.customerNumber) || 0;
      let group = byCustomer.get(customerNumber);
      if (!group) {
        group = { customerNumber, customerName: invoice.customerName, invoices: [], availableEmails: [], selectedEmails: [], adHocEmail: '', loadingEmails: customerNumber > 0 };
        byCustomer.set(customerNumber, group);
      }
      group.invoices.push(invoice);
    }
    this.emailGroups = Array.from(byCustomer.values());
    this.emailResults = null;
    this.emailError = '';
    this.emailModalOpen = true;
    for (const group of this.emailGroups) {
      if (group.customerNumber > 0) {
        this.http.get<string[]>(`/api/customers/${group.customerNumber}/emails`).subscribe({
          next: emails => { group.availableEmails = emails || []; group.loadingEmails = false; },
          error: () => { group.loadingEmails = false; }
        });
      } else {
        group.loadingEmails = false;
      }
    }
  }

  closeEmailModal() { this.emailModalOpen = false; this.emailGroups = []; this.emailResults = null; this.emailError = ''; }
  isGroupEmailSelected(group: EmailGroup, email: string) { return group.selectedEmails.includes(email); }
  toggleGroupEmail(group: EmailGroup, email: string, checked: boolean) {
    group.selectedEmails = checked ? [...group.selectedEmails, email] : group.selectedEmails.filter(e => e !== email);
  }
  groupInvoiceLabel(group: EmailGroup) { return group.invoices.map(i => i.invoiceNumber).join(', '); }
  hasSelectableEmails() {
    return this.emailGroups.some(group => group.selectedEmails.length > 0 || !!group.adHocEmail.trim());
  }

  sendSelectedInvoiceEmails() {
    this.emailError = '';
    const groups = this.emailGroups
      .map(group => {
        const emails = [...group.selectedEmails];
        const adHoc = group.adHocEmail.trim();
        if (adHoc) emails.push(adHoc);
        return {
          customerNumber: group.customerNumber,
          customerName: group.customerName,
          invoices: group.invoices.map(i => ({ invoiceNumber: i.invoiceNumber, storeNumber: i.storeNumber || 0 })),
          emails
        };
      });

    if (!groups.length) {
      this.emailError = 'Select at least one recipient email address.';
      return;
    }

    this.sendingEmails = true;
    this.emailResults = null;
    this.http.post<InvoiceEmailResult[]>('/api/invoices/email', { groups }).subscribe({
      next: results => {
        this.emailResults = results;
        this.sendingEmails = false;
        if (results.length > 0 && results.every(r => r.success)) {
        }
      },
      error: e => {
        this.sendingEmails = false;
        this.emailError = e.error?.message || e.error || 'Unable to send emails.';
      }
    });
  }

  // Sales Methods
  getAccountName(c: any): string {
    if (!c) return '';
    if (typeof c === 'string') return c;
    return c.customerName || c.accountName || c.name || '';
  }

  get accountNameOptions(): string[] {
    const uniqueNames = new Map<string, string>();
    for (const customer of [...this.salesCustomers, ...this.allCustomers]) {
      const accountName = this.getAccountName(customer).trim();
      if (accountName && !uniqueNames.has(accountName.toLowerCase())) uniqueNames.set(accountName.toLowerCase(), accountName);
    }
    return Array.from(uniqueNames.values()).sort((a, b) => a.localeCompare(b));
  }

  private getAssignedRepEmails(customer: any): string[] {
    if (!customer) return [];
    const assigned = customer.assignedSalesReps ?? customer.assigned_sales_reps ?? customer.assignedRepEmails ?? customer.assigned_rep_emails;
    if (Array.isArray(assigned)) {
      return assigned
        .map(rep => typeof rep === 'string' ? rep : rep?.repEmail || rep?.email || '')
        .map(rep => rep.trim().toLowerCase())
        .filter(Boolean);
    }
    if (typeof assigned === 'string' && assigned.trim()) return [assigned.trim().toLowerCase()];
    return [];
  }

  applyAdminAccountFilter() {
    let list = [...this.salesCustomers];
    const q = this.adminAccountFilter.trim().toLowerCase();
    if (q) {
      list = list.filter(c => this.getAccountName(c).toLowerCase().includes(q));
    }
    if (this.adminRepFilter) {
      if (this.adminRepFilter === '__unassigned__') {
        list = list.filter(c => !c.assignedSalesReps || !c.assignedSalesReps.length);
      } else {
        const filterRep = this.adminRepFilter.toLowerCase();
        list = list.filter(c => (c.assignedSalesReps || []).some(r => r.toLowerCase() === filterRep));
      }
    }
    this.filteredSalesCustomers = list;
  }

  loadSalesData() {
    if (!this.allCustomers.length) {
      this.loadCustomers();
    }

    this.salesService.getReps().subscribe({
      next: reps => {
        this.salesReps = (reps || []).map(r => {
          if (typeof r === 'string') {
            return { id: 0, repName: r, name: r, repEmail: r, email: r, status: 'A' };
          }
          const repName = r.repName || r.name || r.rep_name || '';
          const repEmail = r.repEmail || r.email || r.rep_email || '';
          return { ...r, repName, name: repName, repEmail, email: repEmail, status: r.status || 'A' };
        });

        if (!this.isSalesAdmin && this.currentUserEmail) {
          this.selectedSalesFilterRep = this.currentUserEmail;
        }
        this.loadScheduledCalls();
      },
      error: () => this.loadScheduledCalls()
    });

    this.salesService.getCustomers().subscribe({
      next: custs => {
        this.salesCustomers = (custs || []).map(c => {
          if (typeof c === 'string') {
            return { customerNumber: 0, customerName: c, accountName: c, assignedSalesReps: [] };
          }
          const customerName = c.customerName || c.accountName || c.customer_name || c.name || '';
          return {
            ...c,
            customerName,
            accountName: customerName,
            assignedSalesReps: Array.isArray(c.assignedSalesReps) ? c.assignedSalesReps : (Array.isArray(c.assigned_sales_reps) ? c.assigned_sales_reps : [])
          };
        });
        this.applyAdminAccountFilter();
      },
      error: () => {
        this.salesCustomers = [];
        this.filteredSalesCustomers = [];
      }
    });

    this.salesService.getUnassignedCustomers().subscribe({
      next: custs => {
        this.unassignedSalesCustomers = (custs || [])
          .map(c => {
            if (typeof c === 'string') {
              return { customerNumber: 0, customerName: c, accountName: c, assignedSalesReps: [] };
            }
            const customerName = c.customerName || c.accountName || c.customer_name || c.name || '';
            return {
              ...c,
              customerName,
              accountName: customerName,
              assignedSalesReps: Array.isArray(c.assignedSalesReps) ? c.assignedSalesReps : (Array.isArray(c.assigned_sales_reps) ? c.assigned_sales_reps : [])
            };
          });
      },
      error: () => {
        this.unassignedSalesCustomers = [];
      }
    });
  }

  setSalesTab(tab: 'scheduled' | 'new-call' | 'history' | 'admin') {
    this.salesTab = tab;
    this.callSuccessMessage = '';
    if (tab === 'scheduled') {
      this.loadScheduledCalls();
    } else if (tab === 'history') {
      this.loadCallHistory();
    }
  }

  onFilterRepChange() {
    if (this.salesTab === 'scheduled') {
      this.loadScheduledCalls();
    } else if (this.salesTab === 'history') {
      this.loadCallHistory();
    }
  }

  onSalesFilterRepChange(value: string) {
    this.selectedSalesFilterRep = value;
    if (value && this.salesTab === 'admin') {
      this.salesTab = 'scheduled';
    }
  }

  handleNewCallSaved(call: SalesCall) {
    this.showCallStatusTab(call.status);
    this.callSuccessMessage = 'Call saved successfully!';
    if (this.callSuccessToastTimer !== null) window.clearTimeout(this.callSuccessToastTimer);
    this.callSuccessToastTimer = window.setTimeout(() => {
      this.callSuccessMessage = '';
      this.callSuccessToastTimer = null;
    }, 4000);
  }

  private showCallStatusTab(status?: number) {
    const tab = status === 0 || status === 2 ? 'scheduled' : 'history';
    this.setSalesTab(tab);
  }

  loadScheduledCalls() {
    this.loadingScheduledCalls = true;
    const params: Record<string, string> = {};
    if (this.selectedSalesFilterRep) params['salesRepEmail'] = this.selectedSalesFilterRep;
    this.salesService.getUpcomingCalls(params).subscribe({
      next: calls => {
        this.scheduledCalls = calls || [];
        this.scheduledAccountOptions = [...new Set(this.scheduledCalls.map(c => (c.accountName || '').trim()).filter(Boolean))].sort((a, b) => a.localeCompare(b));
        this.loadingScheduledCalls = false;
      },
      error: () => {
        this.scheduledCalls = [];
        this.loadingScheduledCalls = false;
      }
    });
  }

  isAccountProspect(accountName: string): boolean {
    if (!accountName) return false;
    return !this.salesCustomers.some(c => (c.customerName || c.accountName || '').toLowerCase() === accountName.trim().toLowerCase());
  }

  isCallProspect(call: SalesCall): boolean {
    return !!call && this.isAccountProspect(call.accountName);
  }

  openCompleteModal(call: SalesCall) {
    this.completingCall = call;
    this.completingComments = call.comments || '';
    this.completingFollowUpDate = call.followUpDate ? call.followUpDate.slice(0, 10) : '';
  }

  saveCompleteCall() {
    if (!this.completingCall || (!this.completingCall.id && !this.completingCall.callID)) return;
    const callId = this.completingCall.id || this.completingCall.callID;
    const updated: SalesCall = {
      ...this.completingCall,
      status: 1,
      comments: this.completingComments,
      followUpDate: this.completingFollowUpDate || undefined
    };
    this.salesService.updateCall(Number(callId), updated).subscribe({
      next: () => {
        this.completingCall = null;
        this.loadScheduledCalls();
      },
      error: () => {}
    });
  }

  openCallDetails(call: SalesCall) {
    this.selectedCallDetails = call;
  }

  convertProspectToCustomer() {
    const call = this.selectedCallDetails;
    const callId = call?.callID || Number(call?.id);
    if (!call || !callId || this.convertingProspect) return;

    this.convertingProspect = true;
    this.salesService.convertProspect(Number(callId)).subscribe({
      next: () => {
        call.isProspect = false;
        this.convertingProspect = false;
        this.callSuccessMessage = 'Customer added successfully!';
        if (this.callSuccessToastTimer !== null) window.clearTimeout(this.callSuccessToastTimer);
        this.callSuccessToastTimer = window.setTimeout(() => {
          this.callSuccessMessage = '';
          this.callSuccessToastTimer = null;
        }, 4000);
        this.closeCallDetails();
        this.loadSalesData();
        if (this.salesTab === 'history') this.loadCallHistory();
      },
      error: () => {
        this.convertingProspect = false;
      }
    });
  }

  closeCallDetails() {
    this.selectedCallDetails = null;
  }

  onCallCardKeydown(event: KeyboardEvent, call: SalesCall) {
    if (event.key === 'Enter' || event.key === ' ') {
      event.preventDefault();
      this.openCallDetails(call);
    }
  }

  openEditCall(call: SalesCall) {
    this.editingCall = {
      ...call,
      phone: call.contactPhone || call.phone || '',
      repEmail: call.salesRepEmail || call.repEmail || '',
      callDate: call.callDate || `${toDateInputValue(new Date())}T00:00:00`,
      followUpDate: call.followUpDate ? call.followUpDate.slice(0, 10) : ''
    };
  }

  saveEditCall() {
    if (!this.editingCall || (!this.editingCall.id && !this.editingCall.callID)) return;
    const callId = this.editingCall.id || this.editingCall.callID;
    const call = {
      ...this.editingCall,
      followUpDate: this.editingCall.followUpDate || undefined
    };
    this.salesService.updateCall(Number(callId), call).subscribe({
      next: () => {
        this.editingCall = null;
        this.showCallStatusTab(call.status);
        this.callSuccessMessage = 'Call updated successfully!';
        if (this.callSuccessToastTimer !== null) window.clearTimeout(this.callSuccessToastTimer);
        this.callSuccessToastTimer = window.setTimeout(() => {
          this.callSuccessMessage = '';
          this.callSuccessToastTimer = null;
        }, 4000);
      },
      error: () => {}
    });
  }

  deleteCall(call: SalesCall) {
    const callId = call.id || call.callID;
    if (!callId || !confirm(`Delete call record for "${call.accountName}"?`)) return;
    this.salesService.deleteCall(Number(callId)).subscribe({
      next: () => {
        if (this.salesTab === 'scheduled') this.loadScheduledCalls();
        if (this.salesTab === 'history') this.loadCallHistory();
      },
      error: () => {}
    });
  }

  loadCallHistory() {
    this.loadingHistory = true;
    this.loadingMoreHistory = false;
    this.historyPage = 1;
    const params: Record<string, string> = {};
    if (this.selectedSalesFilterRep) params['salesRepEmail'] = this.selectedSalesFilterRep;

    if (this.historyViewMode === 'records') {
      if (this.historyDateFrom) params['fromDate'] = this.historyDateFrom;
      if (this.historyDateTo) params['toDate'] = this.historyDateTo;
      if (this.historyAccountFilter.trim()) params['accountFilter'] = this.historyAccountFilter.trim();
      params['page'] = '1';
      params['pageSize'] = '30';
      this.salesService.getCalls(params).subscribe({
        next: result => {
          this.callHistory = result?.calls || [];
          this.historyTotalCount = result?.totalCount || 0;
          this.callHistoryAccountOptions = [...new Set(this.callHistory.map(c => (c.accountName || '').trim()).filter(Boolean))].sort((a, b) => a.localeCompare(b));
          this.applyHistoryFilter();
          this.loadingHistory = false;
        },
        error: () => {
          this.callHistory = [];
          this.filteredCallHistory = [];
          this.callHistoryAccountOptions = [];
          this.historyTotalCount = 0;
          this.loadingHistory = false;
        }
      });
    } else {
      this.salesService.getAccountSummaries(params).subscribe({
        next: summaries => {
          this.accountSummaries = (summaries || []).map(summary => ({
            ...summary,
            totalCalls: summary.totalCalls ?? summary.calls?.length ?? 0,
            scheduledCalls: summary.scheduledCalls ?? summary.calls?.filter(call => call.status === 0).length ?? 0,
            completedCalls: summary.completedCalls ?? summary.calls?.filter(call => call.status === 1).length ?? 0,
            lastCallDate: summary.lastCallDate ?? summary.calls?.[0]?.callDate ?? summary.calls?.[0]?.createdDate,
            calls: summary.calls || []
          }));
          this.callHistoryAccountOptions = [...new Set(this.accountSummaries.map(s => (s.accountName || '').trim()).filter(Boolean))].sort((a, b) => a.localeCompare(b));
          this.applyHistoryFilter();
          this.loadingHistory = false;
        },
        error: () => {
          this.accountSummaries = [];
          this.filteredAccountSummaries = [];
          this.callHistoryAccountOptions = [];
          this.loadingHistory = false;
        }
      });
    }
  }

  loadMoreHistory() {
    if (this.loadingHistory || this.loadingMoreHistory || this.callHistory.length >= this.historyTotalCount) return;

    this.loadingMoreHistory = true;
    const params: Record<string, string> = {
      page: String(this.historyPage + 1),
      pageSize: '30'
    };
    if (this.selectedSalesFilterRep) params['salesRepEmail'] = this.selectedSalesFilterRep;
    if (this.historyDateFrom) params['fromDate'] = this.historyDateFrom;
    if (this.historyDateTo) params['toDate'] = this.historyDateTo;
    if (this.historyAccountFilter.trim()) params['accountFilter'] = this.historyAccountFilter.trim();

    this.salesService.getCalls(params).subscribe({
      next: result => {
        this.historyPage += 1;
        this.callHistory = [...this.callHistory, ...(result?.calls || [])];
        this.filteredCallHistory = [...this.callHistory];
        this.loadingMoreHistory = false;
      },
      error: () => {
        this.loadingMoreHistory = false;
      }
    });
  }

  onHistoryDateRangeChange(range: { dateFrom: string; dateTo: string }) {
    this.historyDateFrom = range.dateFrom;
    this.historyDateTo = range.dateTo;
  }

  onHistoryAccountSearchChange() {
    this.applyHistoryFilter();
  }

  selectAccountSummary(summary: AccountSummary) {
    this.selectedSummaryAccount = summary.accountName;
    this.selectedAccountSummaryCalls = summary.calls || [];
  }

  clearSelectedSummaryAccount() {
    this.selectedSummaryAccount = '';
    this.selectedAccountSummaryCalls = [];
  }

  onSummaryCardKeydown(event: KeyboardEvent, summary: AccountSummary) {
    if (event.key === 'Enter' || event.key === ' ') {
      event.preventDefault();
      this.selectAccountSummary(summary);
    }
  }

  applyHistoryFilter() {
    const q = this.historyAccountFilter.trim().toLowerCase();
    if (!q) {
      this.filteredCallHistory = [...this.callHistory];
      this.filteredAccountSummaries = [...this.accountSummaries];
      return;
    }
    this.filteredCallHistory = this.callHistory.filter(c => (c.accountName || '').toLowerCase().includes(q) || (c.contactName || '').toLowerCase().includes(q));
    this.filteredAccountSummaries = this.accountSummaries.filter(s => (s.accountName || '').toLowerCase().includes(q));
  }

  exportHistoryCsv() {
    const header = ['Account Name', 'Contact Name', 'Phone', 'Call Date', 'Follow-up Date', 'Sales Rep', 'Status', 'Is Prospect', 'Comments'];
    const rows = this.filteredCallHistory.map(c => [
      `"${(c.accountName || '').replace(/"/g, '""')}"`,
      `"${(c.contactName || '').replace(/"/g, '""')}"`,
      `"${(c.contactPhone || c.phone || '').replace(/"/g, '""')}"`,
      `"${c.callDate ? c.callDate.slice(0, 10) : ''}"`,
      `"${c.followUpDate ? c.followUpDate.slice(0, 10) : ''}"`,
      `"${this.getRepDisplayName(c.repName, c.salesRepEmail || c.repEmail).replace(/"/g, '""')}"`,
      `"${c.status === 1 ? 'Completed' : 'Scheduled'}"`,
      `"${c.isProspect ? 'Yes' : 'No'}"`,
      `"${(c.comments || '').replace(/"/g, '""')}"`
    ]);
    const csvContent = [header.join(','), ...rows.map(r => r.join(','))].join('\r\n');
    const blob = new Blob([csvContent], { type: 'text/csv;charset=utf-8;' });
    const url = URL.createObjectURL(blob);
    const link = document.createElement('a');
    link.setAttribute('href', url);
    link.setAttribute('download', `sales_call_history_${new Date().toISOString().slice(0, 10)}.csv`);
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
    URL.revokeObjectURL(url);
  }

  formatAssignedReps(c: any): string {
    if (!c) return 'Unassigned';
    const reps: string[] = typeof c === 'string' ? [] : (Array.isArray(c.assignedSalesReps) ? c.assignedSalesReps : (Array.isArray(c.assigned_sales_reps) ? c.assigned_sales_reps : []));
    if (!reps || !reps.length) return 'Unassigned';
    return reps.map(email => {
      const rep = this.salesReps.find(r => (r.repEmail || r.email || '').toLowerCase() === email.toLowerCase());
      return rep ? (rep.repName || rep.name || rep.repEmail || rep.email || email) : email;
    }).join(', ');
  }

  hasAssignedSalesReps(c: any): boolean {
    if (!c || typeof c === 'string') return false;
    const reps = Array.isArray(c.assignedSalesReps) ? c.assignedSalesReps : c.assigned_sales_reps;
    return Array.isArray(reps) && reps.length > 0;
  }

  getRepDisplayName(repName?: string, repEmail?: string): string {
    if (repName && repName.trim()) return repName;
    if (!repEmail) return '—';
    const rep = this.salesReps.find(r => (r.repEmail || r.email || '').toLowerCase() === repEmail.toLowerCase());
    return rep ? (rep.repName || rep.name || rep.repEmail || rep.email || repEmail) : repEmail;
  }

  printAccountList() {
    const win = window.open('', '_blank');
    if (!win) return;
    const rows = this.salesCustomers.map(c => `<tr><td style="padding:8px;border-bottom:1px solid #ddd;"><b>${this.getAccountName(c)}</b></td><td style="padding:8px;border-bottom:1px solid #ddd;">${this.formatAssignedReps(c)}</td></tr>`).join('');
    win.document.write(`
      <html>
        <head>
          <title>Sales Accounts List - Allen & Kerber Auto Supply</title>
          <style>
            body { font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif; padding: 2rem; color: #172033; }
            table { width: 100%; border-collapse: collapse; margin-top: 1rem; }
            th { text-align: left; padding: 8px; border-bottom: 2px solid #333; }
            h1 { margin-bottom: 0.2rem; }
            p { color: #666; margin-top: 0; }
          </style>
        </head>
        <body>
          <h1>Allen & Kerber Auto Supply</h1>
          <p>Sales Customer Account Assignments &bull; Generated ${new Date().toLocaleDateString()}</p>
          <table>
            <thead><tr><th>Account Name</th><th>Assigned Sales Rep(s)</th></tr></thead>
            <tbody>${rows}</tbody>
          </table>
        </body>
      </html>
    `);
    win.document.close();
    win.focus();
    setTimeout(() => win.print(), 250);
  }

  addSalesRep() {
    const name = (this.newSalesRep.repName || this.newSalesRep.name || '').trim();
    const email = (this.newSalesRep.repEmail || this.newSalesRep.email || '').trim();
    if (!name || !email) return;
    this.salesService.addRep(name, email).subscribe({
      next: () => {
        this.newSalesRep = { repName: '', repEmail: '' };
        this.loadSalesData();
      },
      error: () => {}
    });
  }

  deleteSalesRep(rep: SalesRep) {
    const name = rep.repName || rep.name || '';
    const email = rep.repEmail || rep.email || '';
    if (!email || !confirm(`Remove sales representative "${name || email}"?`)) return;
    this.salesService.deleteRep(email).subscribe({
      next: () => {
        this.loadSalesData();
      },
      error: () => {}
    });
  }

  addSalesCustomer() {
    const name = this.newCustomerName.trim();
    const selectedCustomer = this.unassignedCustomerOptions.find(customer =>
      customer.customerName.trim().toLowerCase() === name.toLowerCase());

    if (!selectedCustomer) {
      this.accountAssignmentMessage = 'A customer must be selected before assigning an account.';
      return;
    }

    if (!this.selectedAssignRepEmail) {
      this.accountAssignmentMessage = 'Select a sales representative before assigning an account.';
      return;
    }

    this.accountAssignmentMessage = '';
    this.salesService.assignCustomer(selectedCustomer.customerName, this.selectedAssignRepEmail).subscribe({
      next: () => {
        this.newCustomerName = '';
        this.selectedAssignRepEmail = '';
        this.accountAssignmentMessage = '';
        this.loadSalesData();
      },
      error: error => {
        this.accountAssignmentMessage = error.error?.message || error.error || 'Unable to assign the selected customer.';
      }
    });
  }

  deleteSalesCustomer(cust: any) {
    const name = this.getAccountName(cust);
    const assignedRepEmails = this.getAssignedRepEmails(cust);
    if (!name || !assignedRepEmails.length || !confirm(`Unassign customer account "${name}"?`)) return;
    forkJoin(assignedRepEmails.map(repEmail => this.salesService.unassignCustomer(name, repEmail))).subscribe({
      next: () => {
        this.loadSalesData();
      },
      error: () => {}
    });
  }

}
