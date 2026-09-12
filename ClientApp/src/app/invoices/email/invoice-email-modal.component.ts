import { CommonModule } from '@angular/common';
import { Component, EventEmitter, Input, Output } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { EmailGroup, InvoiceEmailResult } from '../../shared/models';

@Component({
  selector: 'app-invoice-email-modal',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './invoice-email-modal.component.html',
  styleUrl: './invoice-email-modal.component.css'
})
export class InvoiceEmailModalComponent {
  @Input() groups: EmailGroup[] = [];
  @Input() error = '';
  @Input() results: InvoiceEmailResult[] | null = null;
  @Input() sending = false;
  @Input() isSelected: (group: EmailGroup, email: string) => boolean = () => false;
  @Input() groupLabel: (group: EmailGroup) => string = group => group.invoices.map(invoice => invoice.invoiceNumber).join(', ');
  @Input() canSend = false;

  @Output() closed = new EventEmitter<void>();
  @Output() emailToggled = new EventEmitter<{ group: EmailGroup; email: string; checked: boolean }>();
  @Output() sendRequested = new EventEmitter<void>();
}
