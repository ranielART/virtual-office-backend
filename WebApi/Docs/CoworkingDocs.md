Act as a Senior Software Architect. I need your help designing and planning the implementation of the "Coworking Space" module for a platform called CommunSide.

Technical Stack:

Backend: .NET with Clean Architecture and Entity Framework Core.

Frontend: Next.js / React.

Context & User Flow:

Online Flow: Allows online guest booking without requiring an account or login. Guests select a resource, choose a date/time, pay the required fee, and receive a confirmation email.

Walk-in Flow: The Front Desk handles all operational tasks. Staff inputs the guest's name, times them in, and upon completion, times them out so the system computes the final hours and processes payment.

Extensions: Handled manually by the front desk, requiring staff to check availability and add time to a current session.

Business Rules & Requirements:

Co-working Seats (20 Capacity):

Hourly: ₱50/hr (Regular) | ₱40/hr (Student). Includes open seating, Wi-Fi, power, lounge, coffee.

Passes: Day Pass (₱600/10hrs), Weekly Pass (₱2,700/5 days), Monthly Dedicated Desk (₱8,500/month).

Discounts: Eligible students receive a 10% discount on Day, Weekly, and Monthly passes.

Meeting Rooms:

Executive Room (Max 3 Pax): ₱800/hr | ₱3,000/Half-day (5 hrs) | ₱5,500/Full day (8 hrs).

Collaboration Room (Max 20 Pax): ₱1,500/hr | ₱5,500/Half-day (4 hrs) | ₱9,500/Full day (8 hrs).

Booking & Payment Rules:

A 50% downpayment is required to confirm any advance booking.

No-shows forfeit their downpayment.

Overstays are auto-billed to the session.

Walk-ins are only permitted if slots are free.

Cross-Module Integration: "Virtual Office" (VO) members can use VO credits which are auto-deducted. Premium VO members get priority booking.

Please provide a comprehensive implementation plan covering the following:

Database Entities & Relationships: Outline the necessary database entities (e.g., Bookings, Rooms/Desks, Pricing Packages) and how they relate. Explain how to handle anonymous online guests alongside registered VO Members, and how to track the 50% downpayment versus the pending balance.

Dynamic Pricing & Package Management: Explain how to design the entities so an Admin can fully configure pricing tiers, student discounts, and duration caps without hardcoding. Detail how to link a Booking to a Package so that historical receipts remain accurate even if prices change in the future.

Validation & Capacity Logic: Explain the logic needed to prevent double-booking across different resource types (shared open seats vs. specific dedicated desks vs. private rooms) and how to handle "Premium VO Priority".

Frontend Flow: Outline the step-by-step state management for the Next.js frontend, specifically handling the guest checkout (no login), applying student discounts, and capturing the downpayment.

Edge Cases: Propose solutions for handling:

A guest paying the 50% downpayment online but leaving the premises without paying the remaining balance.

A walk-in wanting to switch from an hourly rate to a Day Pass midway through their stay.

A booking collision where an online guest tries to book a seat that a walk-in is currently occupying but has not yet checked out of.




Answers for the questions:
Polished Answers & Architecture Decisions
1. Payment gateway for online bookings

Polished Answer: We will use a hybrid approach. For manual payments (GCash, Maya, Bank Transfer), users will upload a screenshot, requiring Front Desk validation. For automated payments, we will integrate Paymongo.

Architectural Impact: The Payment entity needs a PaymentMethod enum (Manual, Paymongo), a ProofOfPaymentUrl (nullable string for image uploads), a ReferenceNumber, and a PaymentStatus (Pending Validation, Paid, Failed).

2. VO Credits

Polished Answer: Deferred. VO members will eventually have specific allocations (e.g., Coworking Access: 4 hrs/month for max 2 pax). However, since the Virtual Office module is not yet implemented, we will omit this logic from the MVP.

Architectural Impact: We won't build the Wallet or Credits tables yet, but we should ensure our CalculateFeeQuery is structured so we can easily inject a "Credit Deduction" discount step later.

3. Priority booking for Premium VO members

Polished Answer: Deferred. Like the VO Credits, this feature will be shelved until the broader Virtual Office module is built.

4. No-show detection & Background Jobs

Polished Answer: We will implement a scheduled background job (e.g., using Hangfire or Quartz.NET) combined with manual Front Desk overrides.

Grace Period Reminder: If a guest hasn't checked in 10 minutes past their start time, the system automatically emails the guest and alerts the Front Desk UI. This catches edge cases where the guest is physically present but the Front Desk forgot to click "Time In."

Auto-Forfeit: If the guest still hasn't checked in after a set limit (e.g., 30 or 60 minutes), the system auto-marks the booking as No-Show, forfeits the downpayment, and frees up the seat.

Architectural Impact: Requires a background worker service monitoring BookingStartTime.

Gemini said
Here is the updated and polished answer for number 5, incorporating the seat manager for future scalability:

5. Seat assignment & Expansion

Polished Answer: Seats will be treated as specific, individually labeled resources rather than just a total headcount. To support future growth, this will be controlled by a dynamic "Seat/Resource Manager" within the Admin panel's Co-Working & Room Configuration settings.

Architectural Impact: The Resources table will initially be seeded with 20 distinct rows (e.g., "Seat 01" through "Seat 20"). The Admin module will include a CRUD (Create, Read, Update, Delete) interface allowing authorized staff to easily add new seats (e.g., "Seat 21", "Seat 22") if the business expands, temporarily disable a seat if it needs maintenance, or reconfigure the layout without requiring any new code or database migrations.

6. Student verification

Polished Answer: Verification is required for the discounted rates. Online bookings require an ID upload, while walk-ins require physical presentation to the Front Desk.

Architectural Impact: The Booking or Guest entity needs a StudentIdImageUrl (nullable string) and an IsStudentVerified (boolean). For online bookings, the status remains pending until the Front Desk verifies the uploaded image.

7. Confirmation email content

Polished Answer: The confirmation email should serve as a digital pass and a receipt.

Suggested Fields:

Booking Reference Number: (e.g., #BKG-1042)

Guest Name

Resource Booked: (e.g., Coworking - Seat 12)

Date & Time: (e.g., Oct 24, 2026 | 10:00 AM - 2:00 PM)

Payment Breakdown: Total Fee, Downpayment Paid (50%), Balance Due at Check-in.

Check-in Instructions: Reminder to bring Student ID (if applicable) and a note about the no-show forfeiture policy.

Front Desk Contact: Phone number and email for the specific branch so they can call if running late.

8. Extension rules (Based on your provided image)

Polished Answer: Based on the flowchart, the system operates on a "Remind -> Check -> Add Time" loop. Because extensions are manual and depend on the Front Desk checking upcoming schedules, extensions are strictly subject to availability.

Workflow: 15 minutes before a session ends, the system triggers the "Remind" state (notifying Front Desk and/or the guest). If the guest wants to stay ("Done? -> No"), the Front Desk checks if "Seat X" is booked by someone else next. If it is free, they trigger "Add Time".

Pricing Logic: Extensions should be billed as separate line items at the standard Hourly Rate (or Student Hourly Rate). For example, if they bought a 4-hour block and extend by 2 hours, the checkout invoice shows: Base Package (4hrs) + Extension (2hrs).

Architectural Impact: The Booking entity needs to support ExtendedMinutes or a one-to-many relationship with a BookingExtensions table to track added time and additional costs before the final "Time out and Compute hours" step occurs.