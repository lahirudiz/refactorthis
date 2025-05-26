using System;
using System.Linq;
using RefactorThis.Persistence;

namespace RefactorThis.Domain {
    public class InvoiceService {
        
        private readonly InvoiceRepository _invoiceRepository;

        public InvoiceService(InvoiceRepository invoiceRepository) {
            _invoiceRepository = invoiceRepository;
        }

        public string ProcessPayment(Payment payment) {
            var invoice = _invoiceRepository.GetInvoice(payment.Reference)
                          ?? throw new InvalidOperationException("There is no invoice matching this payment");

            string result;

            if (invoice.Amount == 0) {
                result = HandleZeroAmount(invoice);
            }
            else if (HasExistingPayments(invoice)) {
                result = HandleExistingPayments(invoice, payment);
            }
            else {
                result = HandleNewPayment(invoice, payment);
            }

            invoice.Save();
            return result;
        }

        private bool HasExistingPayments(Invoice invoice) {
            return invoice.Payments != null && invoice.Payments.Any();
        }

        private string HandleZeroAmount(Invoice invoice) {
            if (!HasExistingPayments(invoice))
                return "no payment needed";

            throw new InvalidOperationException(
                "The invoice is in an invalid state, it has an amount of 0 and it has payments.");
        }

        private string HandleExistingPayments(Invoice invoice, Payment payment) {
            var totalPaid = invoice.Payments.Sum(x => x.Amount);
            var remaining = invoice.Amount - invoice.AmountPaid;

            if (totalPaid != 0 && totalPaid == invoice.Amount)
                return "invoice was already fully paid";

            if (totalPaid != 0 && payment.Amount > remaining)
                return "the payment is greater than the partial amount remaining";

            bool isFinal = payment.Amount == remaining;
            ApplyPayment(invoice, payment);

            return isFinal
                ? "final partial payment received, invoice is now fully paid"
                : "another partial payment received, still not fully paid";
        }

        private string HandleNewPayment(Invoice invoice, Payment payment) {
            if (payment.Amount > invoice.Amount)
                return "the payment is greater than the invoice amount";

            bool isFull = payment.Amount == invoice.Amount;
            ApplyPayment(invoice, payment);

            if (isFull)
                return "invoice is now fully paid";

            return "invoice is now partially paid";
        }

        private void ApplyPayment(Invoice invoice, Payment payment) {
            invoice.AmountPaid += payment.Amount;
            if (invoice.Type == InvoiceType.Commercial) {
                invoice.TaxAmount += payment.Amount * 0.14m;
            }
            invoice.Payments.Add(payment);
        }
    }
}
