import { Component, EventEmitter, Output } from '@angular/core';

export type Workspace = 'sales' | 'invoice';

@Component({
  selector: 'app-workspace-chooser',
  standalone: true,
  templateUrl: './workspace-chooser.component.html',
  styleUrls: ['./workspace-chooser.component.css']
})
export class WorkspaceChooserComponent {
  @Output() workspaceSelected = new EventEmitter<Workspace>();

  selectWorkspace(workspace: Workspace) {
    this.workspaceSelected.emit(workspace);
  }
}