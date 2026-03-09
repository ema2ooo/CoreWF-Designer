namespace System.Runtime
{
    internal static class FxCop
    {
        public static class Category
        {
            public const string Design = "Microsoft.Design";
            public const string Naming = "Microsoft.Naming";
            public const string Performance = "Microsoft.Performance";
            public const string Security = "Microsoft.Security";
            public const string Usage = "Microsoft.Usage";
            public const string ReliabilityBasic = "Reliability";
            public const string Xaml = "XAML";
        }

        public static class Rule
        {
            public const string AvoidUncalledPrivateCode = "CA1811:AvoidUncalledPrivateCode";
            public const string CollectionPropertiesShouldBeReadOnly = "CA2227:CollectionPropertiesShouldBeReadOnly";
            public const string ConsiderPassingBaseTypesAsParameters = "CA1011:ConsiderPassingBaseTypesAsParameters";
            public const string DefineAccessorsForAttributeArguments = "CA1019:DefineAccessorsForAttributeArguments";
            public const string DoNotCallOverridableMethodsInConstructors = "CA2214:DoNotCallOverridableMethodsInConstructors";
            public const string DoNotCatchGeneralExceptionTypes = "CA1031:DoNotCatchGeneralExceptionTypes";
            public const string DoNotDeclareReadOnlyMutableReferenceTypes = "CA2104:DoNotDeclareReadOnlyMutableReferenceTypes";
            public const string DoNotIgnoreMethodResults = "CA1806:DoNotIgnoreMethodResults";
            public const string FlagsEnumsShouldHavePluralNames = "CA1714:FlagsEnumsShouldHavePluralNames";
            public const string GenericMethodsShouldProvideTypeParameter = "CA1004:GenericMethodsShouldProvideTypeParameter";
            public const string IdentifiersShouldBeSpelledCorrectly = "CA1704:IdentifiersShouldBeSpelledCorrectly";
            public const string IdentifiersShouldHaveCorrectSuffix = "CA1710:IdentifiersShouldHaveCorrectSuffix";
            public const string InitializeReferenceTypeStaticFieldsInline = "CA1810:InitializeReferenceTypeStaticFieldsInline";
            public const string ReviewUnusedParameters = "CA1801:ReviewUnusedParameters";
            public const string TypesShouldHavePublicParameterlessConstructors = "XAML1009:TypesShouldHavePublicParameterlessConstructors";
            public const string UseEventsWhereAppropriate = "CA1030:UseEventsWhereAppropriate";
        }
    }
}
