import React from 'react';
import ReactDOM from 'react-dom/client';
import { BrowserRouter } from 'react-router-dom';
import { AuthProvider } from './context/AuthContext';
import { AppRoutes } from './routes/AppRoutes';
import './styles.css';
import { LocalizationProvider } from './context/LocalizationContext';
import { ToastProvider } from './components/common/Toast';
import { FormValidationFeedback } from './components/common/FormValidationFeedback';
import { SystemTooltips } from './components/common/SystemTooltips';

ReactDOM.createRoot(document.getElementById('root')!).render(
  <React.StrictMode>
    <BrowserRouter>
      <LocalizationProvider>
        <ToastProvider>
          <FormValidationFeedback><SystemTooltips><AuthProvider><AppRoutes /></AuthProvider></SystemTooltips></FormValidationFeedback>
        </ToastProvider>
      </LocalizationProvider>
    </BrowserRouter>
  </React.StrictMode>,
);
